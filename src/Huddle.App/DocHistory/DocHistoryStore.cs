using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.DocHistory;

/// <summary>
/// Owns <c>{DataDir}/doc-history/</c>: one JSON Lines file per Markdown document, at
/// <c>{rootId}/{relativePath}.jsonl</c>, so the tree mirrors the Library's and a folder rename is one
/// directory move (DocHistory spec §6.2). Only the App writes here, so the store's one lock is all the
/// concurrency control there is. The files are for the App, not the Human.
/// </summary>
internal sealed class DocHistoryStore(IOptions<TeamOptions> options, ILogger<DocHistoryStore> logger, IFeedPublisher? feed = null)
{
    /// <summary>The <c>v</c> every line is written with. A line with any other value is skipped on read.</summary>
    internal const int FormatVersion = 1;

    /// <summary>The extension added to a document's relative path to name its history file.</summary>
    private const string Extension = ".jsonl";

    /// <summary>
    /// One line per entry, so not indented; camelCase names and enum values; relaxed escaping keeps a
    /// summary with an em dash or an arrow readable in the file.
    /// </summary>
    private static readonly JsonSerializerOptions LineOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>UTF-8 without a byte-order mark, for appends.</summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock gate = new();

    /// <summary>History files whose unreadable lines were already reported, so each is logged once per process.</summary>
    private readonly HashSet<string> warnedFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after an append or a move, outside the lock, once per affected path.</summary>
    internal event Action<LibraryPath>? Changed;

    /// <summary>Whether History is on for this installation (<c>Team:DocHistory:Enabled</c>), for the components that show it.</summary>
    internal bool Enabled => options.Value.DocHistory.Enabled;

    /// <summary>How close consecutive Human saves must be to share one row (<c>Team:DocHistory:HumanSaveGroupWindow</c>).</summary>
    internal TimeSpan HumanSaveGroupWindow => options.Value.DocHistory.HumanSaveGroupWindow;

    /// <summary>The folder holding every history file, <c>{DataDir}/doc-history/</c>. Created on first append.</summary>
    private string Folder => Path.Combine(options.Value.DataDir, "doc-history");

    /// <summary>Every entry for <paramref name="file"/>, oldest first. A missing file is an empty list; an unreadable line is skipped.</summary>
    /// <param name="file">The document.</param>
    /// <returns>The entries, oldest first.</returns>
    internal IReadOnlyList<DocHistoryEntry> Read(LibraryPath file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string path = this.FileFor(file);
        lock (this.gate)
        {
            if (!File.Exists(path))
            {
                return [];
            }

            try
            {
                return this.ParseAll(path, File.ReadAllLines(path, Utf8NoBom));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A scanner, a backup or a sync tool can hold the file for a moment. The document must still
                // open, so this reads as no history, and the file is reported once.
                if (this.warnedFiles.Add(path))
                {
                    logger.LogWarning(exception, "Could not read the document history {Path}.", path);
                }

                return [];
            }
        }
    }

    /// <summary>
    /// Appends <paramref name="entry"/> to <paramref name="file"/>'s history. When the file then holds more than
    /// <see cref="DocHistoryOptions.MaxEntries"/> lines, it is rewritten atomically keeping the newest ones.
    /// </summary>
    /// <param name="file">The document.</param>
    /// <param name="entry">The entry to add.</param>
    internal void Append(LibraryPath file, DocHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(entry);

        string path = this.FileFor(file);
        string line = Serialize(entry);
        int max = options.Value.DocHistory.MaxEntries;
        lock (this.gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? this.Folder);
            File.AppendAllText(path, (EndsMidLine(path) ? "\n" : string.Empty) + line + "\n", Utf8NoBom);

            if (max > 0)
            {
                string[] lines = File.ReadAllLines(path, Utf8NoBom);
                if (lines.Length > max)
                {
                    AtomicFile.WriteText(path, string.Join('\n', lines[^max..]) + "\n");
                }
            }
        }

        EventRaise.Isolated(this.Changed, file, ex => logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        PostToFeed(file, entry);
    }

    /// <summary>
    /// Tells the Feed an Agent or a Helper changed a document. The Human's own saves are not posted (Feed spec D-10): one editing
    /// session would be dozens of items. A Helper's write is posted as Huddle's, under the Helper's name.
    /// </summary>
    /// <param name="file">The document.</param>
    /// <param name="entry">The entry just appended.</param>
    private void PostToFeed(LibraryPath file, DocHistoryEntry entry)
    {
        if (feed is null || entry.ActorKind == DocActorKind.Human)
        {
            return;
        }

        string kind = entry.Kind switch
        {
            DocChangeKind.Created => "created",
            DocChangeKind.Edited => "edited",
            DocChangeKind.Reviewed => "reviewed",
            _ => "objected",
        };
        feed.Post(new FeedMessage(
            FeedSourceType.DocumentChanged,
            file.FullPath,
            new FeedActor(entry.ActorKind == DocActorKind.Helper ? FeedActorKind.Huddle : FeedActorKind.Agent, entry.Actor),
            false,
            new DocumentChangedPayload(file.FullPath, file.Root.DisplayName, file.RelativePath, kind, entry.Summary)));
    }

    /// <summary>
    /// Moves the history of <paramref name="from"/> to <paramref name="to"/>: one file for a document, the
    /// mirrored folder for a folder. A missing source is a no-op. An existing destination is replaced, because the
    /// Library refuses a move onto an existing item, so anything there is a stale history.
    /// </summary>
    /// <param name="from">The item before the move.</param>
    /// <param name="to">The item after the move.</param>
    internal void Move(LibraryPath from, LibraryPath to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        bool moved;
        lock (this.gate)
        {
            moved = from.IsFile ? this.MoveFile(this.FileFor(from), this.FileFor(to)) : this.MoveFolder(this.FolderFor(from), this.FolderFor(to));
        }

        if (moved)
        {
            EventRaise.Isolated(this.Changed, from, ex => logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
            EventRaise.Isolated(this.Changed, to, ex => logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>
    /// Deletes the history file of <paramref name="file"/>. A file with no history is a no-op. An IO failure is
    /// logged at Warning and swallowed. <see cref="Changed"/> is raised only when a file was removed.
    /// </summary>
    /// <param name="file">The document whose history goes.</param>
    internal void Delete(LibraryPath file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string path = this.FileFor(file);
        bool deleted = false;
        lock (this.gate)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted = true;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not delete the history file {Path}.", path);
            }
        }

        if (deleted)
        {
            EventRaise.Isolated(this.Changed, file, ex => logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
    }

    /// <summary>Parses one line. A blank line, a line that is not an entry, or one with another <c>v</c> is <see langword="false"/>.</summary>
    /// <param name="line">One line of a history file.</param>
    /// <param name="entry">The entry, when the line is one.</param>
    /// <returns>Whether the line held an entry this version reads.</returns>
    internal static bool TryParseLine(string line, [NotNullWhen(true)] out DocHistoryEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        HistoryLine? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<HistoryLine>(line, LineOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (parsed is not { V: FormatVersion, Actor: { Length: > 0 } actor, Kind: { } kind, ActorKind: { } actorKind })
        {
            return false;
        }

        entry = new DocHistoryEntry(parsed.At, actor, actorKind, kind, parsed.Summary ?? string.Empty);
        return true;
    }

    /// <summary>Writes one entry as one line, with the current <see cref="FormatVersion"/>.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The JSON line, without a line break.</returns>
    internal static string Serialize(DocHistoryEntry entry) =>
        JsonSerializer.Serialize(new HistoryLine(FormatVersion, entry.At, entry.Actor, entry.ActorKind, entry.Kind, entry.Summary), LineOptions);

    /// <summary>Whether a history file ends in a partial line: it has content and its last byte is not a line feed, as after a crash mid-append or a hand edit.</summary>
    /// <param name="path">The history file, which may not exist yet.</param>
    /// <returns><see langword="true"/> when the next entry would be glued onto the last line.</returns>
    private static bool EndsMidLine(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length == 0)
        {
            return false;
        }

        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() != '\n';
    }

    /// <summary>Whether two paths name the same place and differ only in letter case, which a case-insensitive file system treats as one item.</summary>
    /// <param name="source">The path before the move.</param>
    /// <param name="destination">The path after the move.</param>
    /// <returns><see langword="true"/> for a case-only rename.</returns>
    private static bool IsCaseOnly(string source, string destination) =>
        string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) && !string.Equals(source, destination, StringComparison.Ordinal);

    /// <summary>The history file for a document.</summary>
    /// <param name="file">The document.</param>
    /// <returns>The file's full path.</returns>
    internal string FileFor(LibraryPath file) => this.FolderFor(file) + Extension;

    /// <summary>The mirrored path for a Library item, without the extension.</summary>
    /// <param name="item">A document or a folder.</param>
    /// <returns>The full path under the store's folder.</returns>
    private string FolderFor(LibraryPath item)
    {
        string relative = item.RelativePath.Replace('\\', '/').Trim('/');
        string[] segments = relative.Length == 0 ? [] : relative.Split('/');
        return Path.Combine([this.Folder, item.Root.Id, .. segments]);
    }

    /// <summary>Moves one history file, replacing a stale destination.</summary>
    /// <param name="source">The history file before the move.</param>
    /// <param name="destination">The history file after the move.</param>
    /// <returns>Whether anything moved.</returns>
    private bool MoveFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? this.Folder);
        if (IsCaseOnly(source, destination))
        {
            // On a case-insensitive file system the destination IS the source, so overwriting would race itself.
            string temporary = source + ".renaming-" + Guid.NewGuid().ToString("N");
            File.Move(source, temporary);
            File.Move(temporary, destination);
            return true;
        }

        File.Move(source, destination, overwrite: true);
        return true;
    }

    /// <summary>Moves a mirrored folder, replacing a stale destination.</summary>
    /// <param name="source">The mirrored folder before the move.</param>
    /// <param name="destination">The mirrored folder after the move.</param>
    /// <returns>Whether anything moved.</returns>
    private bool MoveFolder(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? this.Folder);
        if (IsCaseOnly(source, destination))
        {
            // On a case-insensitive file system the destination IS the source: deleting it would delete the history.
            string temporary = source + ".renaming-" + Guid.NewGuid().ToString("N");
            Directory.Move(source, temporary);
            Directory.Move(temporary, destination);
            return true;
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.Move(source, destination);
        return true;
    }

    /// <summary>Parses every line, skipping the unreadable ones and logging them once per file.</summary>
    /// <param name="path">The history file, for the log.</param>
    /// <param name="lines">Its lines.</param>
    /// <returns>The readable entries, in file order.</returns>
    private List<DocHistoryEntry> ParseAll(string path, string[] lines)
    {
        List<DocHistoryEntry> entries = new(lines.Length);
        int skipped = 0;
        foreach (string line in lines)
        {
            if (TryParseLine(line, out DocHistoryEntry? entry))
            {
                entries.Add(entry);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                skipped++;
            }
        }

        if (skipped > 0 && this.warnedFiles.Add(path))
        {
            logger.LogWarning("Skipped {Skipped} unreadable line(s) in the document history {Path}.", skipped.ToString(CultureInfo.InvariantCulture), path);
        }

        return entries;
    }

    /// <summary>The on-disk shape of one line (DocHistory spec §6.2). Nullable members so a hand-damaged line parses to a refusal, not an exception.</summary>
    /// <param name="V">The line's format version.</param>
    /// <param name="At">When the entry was written.</param>
    /// <param name="Actor">The author's Name.</param>
    /// <param name="ActorKind">Agent or Human.</param>
    /// <param name="Kind">What the author did.</param>
    /// <param name="Summary">The author's words.</param>
    private sealed record HistoryLine(int V, DateTimeOffset At, string? Actor, DocActorKind? ActorKind, DocChangeKind? Kind, string? Summary);
}
