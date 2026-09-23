using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// Owns <c>{DataDir}/file-state/</c>, one JSON file per Agent, keyed by the Agent's Name, per
/// FC §6.6. <see cref="Update"/> runs a whole read-modify-write under this store's lock, so two
/// concurrent commits from different Rooms never lose one another (finding P-18).
/// </summary>
internal sealed class FileStateStore(IOptions<TeamOptions> options, ILogger<FileStateStore> logger)
{
    /// <summary>
    /// Derived from <see cref="ProtocolJson.Options"/>, matching <see cref="Prompts.PromptStore.IndentedJsonOptions"/>'s
    /// reasoning: this file is written to disk and hand-editable, so it is indented, and
    /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> keeps a Room id or path with an
    /// em-dash readable instead of escaped to <c>\uXXXX</c>.
    /// </summary>
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Lock gate = new();

    /// <summary>The folder holding every Agent's file, <c>{DataDir}/file-state/</c>. Created on first save.</summary>
    private string Folder => Path.Combine(options.Value.DataDir, "file-state");

    /// <summary>
    /// Loads <paramref name="name"/>'s state. A missing file is <see langword="null"/>; a file
    /// that fails to parse is logged at Warning and also <see langword="null"/> - a corrupt state
    /// costs one Turn's list, never a crash.
    /// </summary>
    /// <param name="name">The Agent's Name.</param>
    internal FileState? Load(string name)
    {
        lock (this.gate)
        {
            return this.LoadCore(name);
        }
    }

    /// <summary>Writes <paramref name="state"/> for <paramref name="name"/>: a temporary file, then an atomic rename over the target.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="state">The state to persist.</param>
    internal void Save(string name, FileState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (this.gate)
        {
            this.SaveCore(name, state);
        }
    }

    /// <summary>
    /// Loads <paramref name="name"/>'s state, applies <paramref name="change"/>, and saves the
    /// result - all inside this store's lock, so a concurrent commit from another caller can
    /// never be lost between this call's read and its write (finding P-18).
    /// </summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="change">Computes the new state from the current one, or <see langword="null"/> when none exists.</param>
    /// <returns>The state that was saved.</returns>
    internal FileState Update(string name, Func<FileState?, FileState> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (this.gate)
        {
            FileState? current = this.LoadCore(name);
            FileState updated = change(current);
            this.SaveCore(name, updated);
            return updated;
        }
    }

    /// <summary>
    /// Moves <paramref name="oldName"/>'s file to <paramref name="newName"/>, then rewrites every
    /// other file's <c>subscribed</c> entries, Room folder keys and Writer keys equal to
    /// <paramref name="oldName"/>, case-insensitively. A Room id is never rewritten.
    /// </summary>
    /// <param name="oldName">The Agent's current Name.</param>
    /// <param name="newName">The Agent's new Name.</param>
    internal void Rename(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        lock (this.gate)
        {
            Directory.CreateDirectory(this.Folder);

            string oldPath = this.PathFor(oldName);
            string newPath = this.PathFor(newName);
            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath, overwrite: true);
            }

            foreach (string file in Directory.EnumerateFiles(this.Folder, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(fileName, newName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                this.RewriteName(file, oldName, replacement: newName);
            }
        }
    }

    /// <summary>Deletes <paramref name="name"/>'s file, then removes it from every other file's <c>subscribed</c>, Room folders and Writers.</summary>
    /// <param name="name">The Agent's Name.</param>
    internal void Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (this.gate)
        {
            Directory.CreateDirectory(this.Folder);

            string path = this.PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            foreach (string file in Directory.EnumerateFiles(this.Folder, "*.json"))
            {
                this.RewriteName(file, name, replacement: null);
            }
        }
    }

    /// <summary>The unlocked read side of <see cref="Load"/> and <see cref="Update"/>.</summary>
    private FileState? LoadCore(string name)
    {
        string path = this.PathFor(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            FileStateDocument? document = JsonSerializer.Deserialize<FileStateDocument>(json, IndentedJsonOptions);
            return document is null ? null : ToFileState(document);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "'{Path}' could not be parsed as File Changes state and was ignored.", path);
            return null;
        }
    }

    /// <summary>The unlocked write side of <see cref="Save"/> and <see cref="Update"/>.</summary>
    private void SaveCore(string name, FileState state)
    {
        Directory.CreateDirectory(this.Folder);

        string path = this.PathFor(name);
        string tmpPath = path + ".tmp";
        string json = JsonSerializer.Serialize(ToDocument(state), IndentedJsonOptions);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>The full path of <paramref name="name"/>'s file, <c>{Folder}/&lt;Name&gt;.json</c>.</summary>
    private string PathFor(string name) => Path.Combine(this.Folder, $"{name}.json");

    /// <summary>
    /// Rewrites one file's <c>subscribed</c> entries, Room folder keys and Writer keys equal to
    /// <paramref name="name"/>, case-insensitively: replaced by <paramref name="replacement"/>
    /// when non-null (a rename), or removed entirely when <see langword="null"/> (a removal).
    /// </summary>
    private void RewriteName(string filePath, string name, string? replacement)
    {
        FileState? state;
        try
        {
            string json = File.ReadAllText(filePath);
            FileStateDocument? document = JsonSerializer.Deserialize<FileStateDocument>(json, IndentedJsonOptions);
            state = document is null ? null : ToFileState(document);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "'{Path}' could not be parsed as File Changes state and was left unchanged.", filePath);
            return;
        }

        if (state is null)
        {
            return;
        }

        List<string> subscribed = [];
        foreach (string entry in state.Subscribed)
        {
            string? rewrittenEntry = Rewrite(entry, name, replacement);
            if (rewrittenEntry is not null)
            {
                subscribed.Add(rewrittenEntry);
            }
        }

        Dictionary<string, RoomBaseline> rooms = new(StringComparer.Ordinal);
        foreach ((string roomId, RoomBaseline baseline) in state.Rooms)
        {
            Dictionary<string, FolderSnapshot> folders = new(FolderSnapshotComparer);
            foreach ((string entry, FolderSnapshot snapshot) in baseline.Folders)
            {
                string? rewritten = Rewrite(entry, name, replacement);
                if (rewritten is not null)
                {
                    folders[rewritten] = snapshot;
                }
            }

            rooms[roomId] = new RoomBaseline(folders);
        }

        Dictionary<string, IReadOnlyDictionary<string, FileWriter>> writers = new(FolderSnapshotComparer);
        foreach ((string entry, IReadOnlyDictionary<string, FileWriter> byPath) in state.Writers)
        {
            string? rewritten = Rewrite(entry, name, replacement);
            if (rewritten is not null)
            {
                writers[rewritten] = byPath;
            }
        }

        string outputJson = JsonSerializer.Serialize(ToDocument(new FileState(subscribed, rooms, writers)), IndentedJsonOptions);
        string tmpPath = filePath + ".tmp";
        File.WriteAllText(tmpPath, outputJson);
        File.Move(tmpPath, filePath, overwrite: true);
    }

    /// <summary>Comparer used to key entry-named dictionaries (Room folders, Writers), matching <see cref="FileState.Empty"/>.</summary>
    private static StringComparer FolderSnapshotComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Returns <paramref name="replacement"/> when <paramref name="entry"/> equals
    /// <paramref name="name"/> case-insensitively; <see langword="null"/> when it equals
    /// <paramref name="name"/> and <paramref name="replacement"/> is <see langword="null"/> (a
    /// removal); otherwise <paramref name="entry"/> unchanged.
    /// </summary>
    private static string? Rewrite(string entry, string name, string? replacement)
    {
        return string.Equals(entry, name, StringComparison.OrdinalIgnoreCase) ? replacement : entry;
    }

    /// <summary>Converts a deserialised <see cref="FileStateDocument"/> into a <see cref="FileState"/>, rebuilding every dictionary with the comparers <see cref="FileState.Empty"/> uses.</summary>
    private static FileState ToFileState(FileStateDocument document)
    {
        Dictionary<string, RoomBaseline> rooms = new(StringComparer.Ordinal);
        foreach ((string roomId, RoomBaselineDocument baseline) in document.Rooms)
        {
            Dictionary<string, FolderSnapshot> folders = new(FolderSnapshotComparer);
            foreach ((string entry, Dictionary<string, FileEntryDocument> files) in baseline.Folders)
            {
                Dictionary<string, FileEntry> entries = new(FolderSnapshot.PathComparer);
                foreach ((string relativePath, FileEntryDocument fileEntry) in files)
                {
                    entries[relativePath] = new FileEntry(fileEntry.Size, fileEntry.ModifiedUtc);
                }

                folders[entry] = new FolderSnapshot(entries);
            }

            rooms[roomId] = new RoomBaseline(folders);
        }

        Dictionary<string, IReadOnlyDictionary<string, FileWriter>> writers = new(FolderSnapshotComparer);
        foreach ((string entry, Dictionary<string, FileWriterDocument> byPath) in document.Writers)
        {
            Dictionary<string, FileWriter> rewritten = new(FolderSnapshot.PathComparer);
            foreach ((string relativePath, FileWriterDocument writer) in byPath)
            {
                rewritten[relativePath] = new FileWriter(writer.RoomId, new FileEntry(writer.Size, writer.ModifiedUtc));
            }

            writers[entry] = rewritten;
        }

        return new FileState(document.Subscribed, rooms, writers);
    }

    /// <summary>Converts a <see cref="FileState"/> into its serialisable <see cref="FileStateDocument"/> shape.</summary>
    private static FileStateDocument ToDocument(FileState state)
    {
        Dictionary<string, RoomBaselineDocument> rooms = new(StringComparer.Ordinal);
        foreach ((string roomId, RoomBaseline baseline) in state.Rooms)
        {
            Dictionary<string, Dictionary<string, FileEntryDocument>> folders = new(StringComparer.Ordinal);
            foreach ((string entry, FolderSnapshot snapshot) in baseline.Folders)
            {
                Dictionary<string, FileEntryDocument> files = new(StringComparer.Ordinal);
                foreach ((string relativePath, FileEntry fileEntry) in snapshot.Files)
                {
                    files[relativePath] = new FileEntryDocument(fileEntry.Size, fileEntry.ModifiedUtc);
                }

                folders[entry] = files;
            }

            rooms[roomId] = new RoomBaselineDocument(folders);
        }

        Dictionary<string, Dictionary<string, FileWriterDocument>> writers = new(StringComparer.Ordinal);
        foreach ((string entry, IReadOnlyDictionary<string, FileWriter> byPath) in state.Writers)
        {
            Dictionary<string, FileWriterDocument> rewritten = new(StringComparer.Ordinal);
            foreach ((string relativePath, FileWriter writer) in byPath)
            {
                rewritten[relativePath] = new FileWriterDocument(writer.RoomId, writer.Entry.Size, writer.Entry.ModifiedUtc);
            }

            writers[entry] = rewritten;
        }

        return new FileStateDocument([.. state.Subscribed], rooms, writers);
    }
}

/// <summary>The JSON shape of a <see cref="FileState"/> file, per FC §6.6.</summary>
/// <param name="Subscribed">Entries added by <c>watch_folder</c>, in call order.</param>
/// <param name="Rooms">Keyed by Room id.</param>
/// <param name="Writers">Keyed by entry, then by relative path.</param>
internal sealed record FileStateDocument(
    List<string> Subscribed,
    Dictionary<string, RoomBaselineDocument> Rooms,
    Dictionary<string, Dictionary<string, FileWriterDocument>> Writers);

/// <summary>The JSON shape of a <see cref="RoomBaseline"/>.</summary>
/// <param name="Folders">Keyed by entry, then by relative path.</param>
internal sealed record RoomBaselineDocument(Dictionary<string, Dictionary<string, FileEntryDocument>> Folders);

/// <summary>The JSON shape of a <see cref="FileEntry"/>.</summary>
internal sealed record FileEntryDocument(long Size, DateTimeOffset ModifiedUtc);

/// <summary>The JSON shape of a <see cref="FileWriter"/>: its own entry's <see cref="FileEntry"/> fields flattened alongside the Room id.</summary>
internal sealed record FileWriterDocument(string RoomId, long Size, DateTimeOffset ModifiedUtc);
