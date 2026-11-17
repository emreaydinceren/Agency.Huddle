using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Builds what the Memory Tidy Helper sees for one memory folder (Memory Tidy spec §6.8): a capped index of every topic file, and the
/// files shown in full from the folder's stored offset. It also keeps the day each file was written (§6.5). A folder that cannot be read
/// gives no input rather than an exception.
/// </summary>
/// <param name="store">Holds the offset and the day rows.</param>
/// <param name="options">Supplies the caps.</param>
/// <param name="timeProvider">Gives the zone in which a file's time becomes a day.</param>
internal sealed class MemoryTidyInputBuilder(MemoryTidyStateStore store, IOptions<TeamOptions> options, TimeProvider timeProvider)
{
    /// <summary>The key of Huddle-wide Memory.</summary>
    internal const string HuddleKey = "huddle";

    /// <summary>The key prefix of an Agent's own Memory.</summary>
    internal const string AgentKeyPrefix = "agent:";

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>Builds the input for one folder.</summary>
    /// <param name="folder">The memory folder.</param>
    /// <param name="folderKey"><c>agent:&lt;Name&gt;</c> or <c>huddle</c>.</param>
    /// <param name="owner">Whose Memory it is, in words for the prompt.</param>
    /// <param name="today">The Human's local date.</param>
    /// <param name="context">The pass this call belongs to.</param>
    /// <returns>The input and what the pass needs, or <see langword="null"/> when the folder cannot be read.</returns>
    public MemoryTidyBuild? Build(MemoryFolder folder, string folderKey, string owner, DateOnly today, MemoryTidyPassContext context)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderKey);
        ArgumentNullException.ThrowIfNull(context);

        bool own = folderKey.StartsWith(AgentKeyPrefix, StringComparison.Ordinal);
        if (this.ReadTopics(folder, folderKey, own) is not { } topics)
        {
            return null;
        }

        MemoryTidyOptions tidy = options.Value.Memory.Tidy;
        int offset = store.GetState(folderKey)?.Offset ?? 0;
        List<Topic> eligible = [.. topics.Where(static topic => !topic.Kept).OrderBy(static topic => topic.Day).ThenBy(static topic => topic.Name, StringComparer.Ordinal)];
        List<MemoryTidyFile> shown = Select(eligible, offset, tidy.MaxInputChars);
        MemoryTidyLimits limits = new(tidy.MaxActions, tidy.MaxDropsPerFolder, tidy.MaxAnswerChars, string.Equals(folderKey, HuddleKey, StringComparison.Ordinal));
        MemoryTidyInput input = new(folderKey, owner, today, IndexLines(topics), shown, limits, context);
        return new MemoryTidyBuild(input, shown.Count, shown.Count == eligible.Count, eligible.Count, FolderFingerprint(topics.Select(static topic => (topic.Name, topic.Version))), offset, topics.Count);
    }

    /// <summary>Where the next call starts: zero when every eligible file fitted, otherwise moved on by the number shown, wrapping at the end.</summary>
    /// <param name="offset">The offset the call started at.</param>
    /// <param name="shown">How many files the call showed.</param>
    /// <param name="eligibleCount">How many files were eligible for full view.</param>
    /// <param name="allFitted">Whether every eligible file was shown.</param>
    /// <returns>The offset to store.</returns>
    public static int NextOffset(int offset, int shown, int eligibleCount, bool allFitted)
    {
        if (allFitted || eligibleCount <= 0)
        {
            return 0;
        }

        return (Math.Max(offset, 0) + shown) % eligibleCount;
    }

    /// <summary>The fingerprint of the input for <c>IHelperAgent.Fingerprint</c>: the key, the date, the index lines, the shown files' names and versions and the limits. Never the context.</summary>
    /// <param name="input">The input.</param>
    /// <returns>Lower-case hex of a SHA-256.</returns>
    public static string Fingerprint(MemoryTidyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        StringBuilder text = new();
        _ = text.Append(input.FolderKey).Append('\n').Append(input.Today.ToString(DayFormat, CultureInfo.InvariantCulture)).Append('\n');
        foreach (string line in input.Index)
        {
            _ = text.Append(line).Append('\n');
        }

        _ = text.Append("--files\n");
        foreach (MemoryTidyFile file in input.Files)
        {
            _ = text.Append(file.File).Append('\t').Append(file.Version).Append('\n');
        }

        MemoryTidyLimits limits = input.Limits;
        _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"--limits\n{limits.MaxActions} {limits.MaxDrops} {limits.MaxAnswerChars} {limits.DescriptionsLocked}\n"));
        return Hash(text.ToString());
    }

    /// <summary>The folder's fingerprint (§6.5): SHA-256 over one line per topic file, its name and version, in ordinal name order.</summary>
    /// <param name="files">The topic files' names and versions.</param>
    /// <returns>Lower-case hex of a SHA-256.</returns>
    public static string FolderFingerprint(IEnumerable<(string Name, string Version)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        StringBuilder text = new();
        foreach ((string name, string version) in files.OrderBy(static file => file.Name, StringComparer.Ordinal))
        {
            _ = text.Append(name).Append('\t').Append(version).Append('\n');
        }

        return Hash(text.ToString());
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Adds files whole, from the offset and wrapping, while the total stays within the cap; a file that does not fit is skipped.</summary>
    private static List<MemoryTidyFile> Select(List<Topic> eligible, int offset, int maxChars)
    {
        List<MemoryTidyFile> shown = [];
        int total = 0;
        int start = eligible.Count == 0 ? 0 : Math.Max(offset, 0) % eligible.Count;
        for (int step = 0; step < eligible.Count; step++)
        {
            Topic topic = eligible[(start + step) % eligible.Count];
            if (total + topic.Text.Length > maxChars)
            {
                continue;
            }

            total += topic.Text.Length;
            shown.Add(new MemoryTidyFile(topic.Name, topic.Version, topic.Text, topic.Day));
        }

        return shown;
    }

    /// <summary>The index lines in name order, capped at 200 lines and 12,000 characters, with a last line counting the rest.</summary>
    private static List<string> IndexLines(List<Topic> topics)
    {
        List<string> lines = [];
        int characters = 0;
        foreach (Topic topic in topics)
        {
            string line = $"{topic.Name} — {topic.Summary} — written {topic.Day.ToString(DayFormat, CultureInfo.InvariantCulture)}{(topic.Kept ? " — kept" : string.Empty)}";
            if (lines.Count == TeamMemoryInputBuilder.MaxIndexLines || characters + line.Length > TeamMemoryInputBuilder.MaxIndexCharacters)
            {
                break;
            }

            lines.Add(line);
            characters += line.Length + 1;
        }

        if (lines.Count < topics.Count)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"[…{topics.Count - lines.Count} more files not shown]"));
        }

        return lines;
    }

    /// <summary>Reads every topic file with its day, writes the rows of files whose version changed and deletes the rows of files that are gone.</summary>
    private List<Topic>? ReadTopics(MemoryFolder folder, string folderKey, bool own)
    {
        try
        {
            Dictionary<string, MemoryTidyFileRow> rows = store.GetFiles(folderKey).ToDictionary(static row => row.File, StringComparer.Ordinal);
            List<string> paths = [.. Directory.EnumerateFiles(folder.Path, "*.md", SearchOption.TopDirectoryOnly)
                .Where(path => IsTopicFile(Path.GetFileName(path), own))
                .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)];

            List<Topic> topics = [];
            foreach (string path in paths)
            {
                topics.Add(this.ReadTopic(path, folderKey, rows));
            }

            foreach (string gone in rows.Keys.Where(name => topics.TrueForAll(topic => !string.Equals(topic.Name, name, StringComparison.Ordinal))))
            {
                store.RemoveFile(folderKey, gone);
            }

            return topics;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder is not there, is not a folder, or a file in it is locked: the pass records "not judged", not a failure.
            return null;
        }
    }

    private static bool IsTopicFile(string name, bool own) =>
        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !MemoryFileRules.IsIndexFileName(name) && !(own && ReportedFile.IsName(name));

    private Topic ReadTopic(string path, string folderKey, Dictionary<string, MemoryTidyFileRow> rows)
    {
        string name = Path.GetFileName(path);
        byte[] bytes = File.ReadAllBytes(path);
        string version = MemoryVersion.Of(bytes);
        // A Windows editor's byte-order mark would sit in front of the "---" and hide the frontmatter from the model.
        string text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');

        DateOnly day;
        if (rows.TryGetValue(name, out MemoryTidyFileRow? row) && string.Equals(row.Version, version, StringComparison.Ordinal))
        {
            day = row.WrittenDay;
        }
        else
        {
            DateTimeOffset written = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(written, timeProvider.LocalTimeZone).DateTime);
            store.PutFile(new MemoryTidyFileRow(folderKey, name, version, day));
        }

        return new Topic(name, version, text, day, MemoryIndex.SummaryFor(path), IsKept(text));
    }

    /// <summary>Whether the frontmatter holds <c>tidy: keep</c>, key and value read ignoring case.</summary>
    private static bool IsKept(string text) =>
        NoteProperties.Read(NoteFrontmatter.Locate(text)).Any(static property =>
            string.Equals(property.Key, "tidy", StringComparison.OrdinalIgnoreCase)
            && property.Shape == NotePropertyShape.Scalar
            && string.Equals(property.Value.Trim(), "keep", StringComparison.OrdinalIgnoreCase));

    private sealed record Topic(string Name, string Version, string Text, DateOnly Day, string Summary, bool Kept);
}
