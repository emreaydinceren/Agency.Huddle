using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Tells an Agent which Huddle-wide facts, the <c>*.md</c> files directly in <c>Teams/memory</c>, are new, changed or gone since
/// its session last heard (#361). The state is the JSON of two maps from file name to content hash: what the session
/// <c>started</c> with and what it has <c>heard</c>, as for AGENTS.md. Before always returns an addition, so After can
/// mark the Agent's own writes heard. No Helper judges a fact: the block names the file and gives its description.
/// </summary>
/// <param name="options">Locates the Teams root.</param>
/// <param name="prompts">Supplies the block's wording.</param>
/// <param name="logger">Receives the warnings for a file or folder that cannot be read and for a state that cannot be parsed.</param>
/// <param name="merges">Says which removed file a Memory Tidy merge moved into another; without it every removed file is announced as removed.</param>
internal sealed class HuddleMemoryTurnHook(IOptions<TeamOptions> options, IPromptSource prompts, ILogger<HuddleMemoryTurnHook> logger, MemoryTidyMergeStore? merges = null)
    : TurnHook<IReadOnlyDictionary<string, string>>
{
    /// <summary>The most files one block lists; the rest follow at the next Turn.</summary>
    internal const int MaxListed = 20;

    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The hook's name, as the pipeline's production order and the session's hook state know it.</summary>
    internal const string HookName = "HuddleMemory";

    /// <inheritdoc />
    public override string Name => HookName;

    private string MemoryFolder => Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir, "memory"));

    /// <inheritdoc />
    public override bool AppliesTo(TurnHookScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return !scope.Clean;
    }

    /// <inheritdoc />
    public override Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);

        switch (opening.Reason)
        {
            case SessionOpenReason.Fresh:
                Dictionary<string, Snapshot>? now = this.Scan(null);
                if (now is null)
                {
                    return Task.FromResult<string?>(null);
                }

                Dictionary<string, string> hashes = HashesOf(now);
                return Task.FromResult<string?>(Serialize(hashes, hashes));
            case SessionOpenReason.Compacted:
                IReadOnlyDictionary<string, string>? started = this.Parse(opening.State)?.Started;
                return Task.FromResult(started is null ? null : Serialize(started, started));
            default:
                return Task.FromResult(opening.State);
        }
    }

    /// <inheritdoc />
    public override Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        StateDocument? state = this.Parse(turn.State);
        Dictionary<string, Snapshot>? now = this.Scan(state?.Heard);
        if (now is null)
        {
            return Task.FromResult<TurnAddition?>(null);
        }

        Dictionary<string, string> current = HashesOf(now);
        if (state?.Heard is null)
        {
            // A state from before the feature, or a folder that was missing at the open: count from first sight, silently.
            return Task.FromResult<TurnAddition?>(new TurnAddition(null, current));
        }

        List<(string Name, FactChange Kind)> changes = DiffOf(state.Heard, current);
        List<(string Name, FactChange Kind)> listed = changes.Count > MaxListed ? changes[..MaxListed] : changes;
        Dictionary<string, string> heard = new(state.Heard, StringComparer.Ordinal);
        foreach ((string name, FactChange kind) in listed)
        {
            if (kind == FactChange.Deleted)
            {
                _ = heard.Remove(name);
            }
            else
            {
                heard[name] = current[name];
            }
        }

        string? block = listed.Count == 0 ? null : this.Render(listed, now, changes.Count - listed.Count);
        return Task.FromResult<TurnAddition?>(new TurnAddition(block, heard));
    }

    /// <inheritdoc />
    protected override Task<string?> AfterTurnAsync(TurnEnded turn, IReadOnlyDictionary<string, string>? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (carry is null)
        {
            return Task.FromResult(turn.State);
        }

        // The Agent's own writes are not news to it: take what is on disk now for each file it touched in the Memory folder.
        Dictionary<string, string> heard = new(carry, StringComparer.Ordinal);
        string folder = this.MemoryFolder;
        bool linked = this.IsLinkedAbove(folder);
        foreach (string path in turn.Touched)
        {
            if (linked || !TryMemoryFileName(path, folder, out string name))
            {
                continue;
            }

            string file = Path.Combine(folder, name);
            string? hash = TryHash(file, out _);
            if (hash is null)
            {
                // Gone is heard; locked or a link keeps what was heard before, so it is not reported New once it reads.
                if (!File.Exists(file))
                {
                    _ = heard.Remove(name);
                }
            }
            else
            {
                heard[name] = hash;
            }
        }

        return Task.FromResult<string?>(Serialize(this.Parse(turn.State)?.Started, heard));
    }

    private static bool TryMemoryFileName(string path, string folder, out string name)
    {
        name = string.Empty;
        try
        {
            string full = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(full);
            if (directory is null || !string.Equals(directory, folder, PathContainment.Comparison))
            {
                return false;
            }

            name = Path.GetFileName(full);
            return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !MemoryFileRules.IsIndexFileName(name);
        }
        catch (ArgumentException)
        {
            // A touched path that is not a valid path cannot be a Memory file.
            return false;
        }
    }

    /// <summary>The content hash of a file, and its description; <see langword="null"/> when it is missing, a link or cannot be read.</summary>
    private static string? TryHash(string file, out string? description)
    {
        description = null;
        try
        {
            if (!File.Exists(file) || TeamMemoryIndex.IsSkipped(file))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(file);
            description = MemoryFileRules.DescriptionOf(Encoding.UTF8.GetString(bytes));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Treated as unreadable by the caller, which keeps what it last heard for the file.
            return null;
        }
    }

    private static Dictionary<string, string> HashesOf(Dictionary<string, Snapshot> now) =>
        now.ToDictionary(static pair => pair.Key, static pair => pair.Value.Hash, StringComparer.Ordinal);

    private static List<(string Name, FactChange Kind)> DiffOf(IReadOnlyDictionary<string, string> heard, Dictionary<string, string> current)
    {
        List<(string Name, FactChange Kind)> changes = [];
        foreach ((string name, string hash) in current)
        {
            if (!heard.TryGetValue(name, out string? before))
            {
                changes.Add((name, FactChange.Added));
            }
            else if (!string.Equals(before, hash, StringComparison.Ordinal))
            {
                changes.Add((name, FactChange.Changed));
            }
        }

        changes.AddRange(heard.Keys.Where(name => !current.ContainsKey(name)).Select(static name => (name, FactChange.Deleted)));
        changes.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return changes;
    }

    private static string Serialize(IReadOnlyDictionary<string, string>? started, IReadOnlyDictionary<string, string> heard) =>
        JsonSerializer.Serialize(new StateDocument(started, heard), StateOptions);

    /// <summary>Renders the block: the header, one line per fact, and the "more" line when the cap left some for the next Turn.</summary>
    private string Render(List<(string Name, FactChange Kind)> listed, Dictionary<string, Snapshot> now, int left)
    {
        StringBuilder builder = new();
        builder.Append(prompts.Render("turn.huddleMemoryHeader", new Dictionary<string, string> { ["{{folder}}"] = this.MemoryFolder }));
        foreach ((string name, FactChange kind) in listed)
        {
            string key = kind switch
            {
                FactChange.Added => "turn.huddleMemoryAdded",
                FactChange.Changed => "turn.huddleMemoryChanged",
                _ => "turn.huddleMemoryDeleted",
            };

            builder.Append('\n');
            if (kind == FactChange.Deleted && merges?.FindInto(name) is { } into)
            {
                // A merge moved the fact: it is still true, in the file the merge kept.
                builder.Append(prompts.Render("turn.huddleMemoryMerged", new Dictionary<string, string> { ["{{into}}"] = into, ["{{name}}"] = name }));
                continue;
            }

            builder.Append(prompts.Render(key, new Dictionary<string, string> { ["{{name}}"] = name }));
            if (kind != FactChange.Deleted && now[name].Description is { } description)
            {
                builder.Append(prompts.Render("turn.huddleMemoryDescription", new Dictionary<string, string> { ["{{description}}"] = OneLine(description) }));
            }
        }

        if (left > 0)
        {
            builder.Append('\n');
            builder.Append(prompts.Render("turn.huddleMemoryMore", new Dictionary<string, string> { ["{{count}}"] = left.ToString(CultureInfo.InvariantCulture) }));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads the <c>*.md</c> files directly in the Memory folder. A link is skipped, as the Team Memory index skips it. A file that
    /// cannot be read keeps its last heard hash, so it is not reported as gone. <see langword="null"/> when the folder cannot be
    /// listed (a link, or an error), which says nothing about the files.
    /// </summary>
    private Dictionary<string, Snapshot>? Scan(IReadOnlyDictionary<string, string>? heard)
    {
        string folder = this.MemoryFolder;
        Dictionary<string, Snapshot> found = new(StringComparer.Ordinal);
        try
        {
            if (this.IsLinkedAbove(folder))
            {
                return null;
            }

            if (!Directory.Exists(folder))
            {
                return found;
            }

            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                // The same case rule as TryMemoryFileName, whatever the file system: .md in any case.
                string name = Path.GetFileName(file);
                if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || MemoryFileRules.IsIndexFileName(name))
                {
                    continue;
                }

                string? hash = TryHash(file, out string? description);
                if (hash is not null)
                {
                    found[name] = new Snapshot(hash, description);
                }
                else if (heard is not null && heard.TryGetValue(name, out string? last))
                {
                    found[name] = new Snapshot(last, null);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The Huddle-wide Memory folder could not be listed; no change is reported this Turn.");
            return null;
        }

        return found;
    }

    /// <summary>Whether the Teams root or the Memory folder is a link, which the Memory paths skip whole.</summary>
    private bool IsLinkedAbove(string folder) =>
        TeamMemoryIndex.IsSkipped(Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir))) || TeamMemoryIndex.IsSkipped(folder);

    /// <summary>One line of the file's own words: line breaks and tabs become single spaces, double quotes single ones, and it is cut to the Memory index's limit.</summary>
    private static string OneLine(string description)
    {
        StringBuilder builder = new(description.Length);
        bool space = false;
        foreach (char c in description.Trim())
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c == '"' ? '\'' : c);
        }

        return builder.Length > MemoryFileRules.MaxDescriptionLength ? builder.ToString(0, MemoryFileRules.MaxDescriptionLength) : builder.ToString();
    }

    /// <summary>Reads a stored state; null for no state, and for one that cannot be parsed, which is logged.</summary>
    private StateDocument? Parse(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StateDocument>(state, StateOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The HuddleMemory hook state could not be read; treating it as unknown.");
            return null;
        }
    }

    private enum FactChange
    {
        Added,
        Changed,
        Deleted,
    }

    private readonly record struct Snapshot(string Hash, string? Description);

    /// <summary>The stored state: what the session started with (null when unknown) and what it has heard.</summary>
    private sealed record StateDocument(
        [property: JsonPropertyName("started")] IReadOnlyDictionary<string, string>? Started,
        [property: JsonPropertyName("heard")] IReadOnlyDictionary<string, string>? Heard);
}
