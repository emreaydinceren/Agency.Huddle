namespace Agency.Huddle.App.Acp;

using System.Collections.Frozen;

/// <summary>
/// A pure, immutable snapshot of every Persona discovered under the Teams directory, built from an
/// in-memory list of (path, text) pairs with no filesystem access of its own - see
/// <see cref="Build(IReadOnlyList{ValueTuple{string, string}})"/>. <see cref="PersonaStore"/> is the
/// only I/O shell around this: it reads files off disk, hands their text here, and swaps the result
/// in as one immutable reference. Building this way also lets the write path
/// (<see cref="PersonaStore.Add"/>, <see cref="PersonaStore.Update"/>) validate a candidate edit -
/// the current entries with one file substituted - before it ever touches disk, using the exact same
/// engine that decides what the read path shows.
/// </summary>
/// <remarks>
/// <para>
/// A file's identity is read with <see cref="PersonaFrontmatter.TryReadIdentity"/>; a file that
/// fails becomes a <see cref="RejectedPersonaFile"/> naming the parser's reason, never an exception -
/// a malformed Persona file is an expected outcome on the read path, not a crash.
/// </para>
/// <para>
/// Beyond that per-file check, three collisions are rejected across the whole set, all compared
/// case-insensitively (matching SQLite's <c>COLLATE NOCASE</c> and <c>MentionParser</c>'s
/// <see cref="StringComparer.OrdinalIgnoreCase"/> - and closing the live bug
/// docs/agencyteam/known-limits.md records, "Jarvis" and "jarvis" sharing one SQLite row): two files
/// sharing a Name, two files sharing an Alias, and one file's Alias equalling a DIFFERENT file's
/// Name. Every file on either side of a collision is rejected - there is no "first one wins" rule,
/// because picking a winner by enumeration order is how an edit to the loser silently does nothing.
/// A file whose Alias equals its own Name is explicitly fine: a plausible authoring choice, not a
/// typo.
/// </para>
/// </remarks>
internal sealed class PersonaIndex
{
    private readonly FrozenDictionary<string, PersonaEntry> byName;
    private readonly FrozenDictionary<string, PersonaEntry> byAlias;

    /// <summary>
    /// Constructs an index directly from already-decided entries, rejections and teams, with no
    /// validation of its own. Not private (unlike a typical factory-backed type) so tests can probe
    /// <see cref="ByName"/> and <see cref="ByNameOrAlias"/>'s lookup precedence in isolation, with a
    /// hand-built <paramref name="entries"/> list that need not have passed through
    /// <see cref="Build"/>'s collision rules - collision detection is <see cref="Build"/>'s job
    /// alone, and production code only ever reaches this type through it.
    /// </summary>
    /// <param name="entries">Every Persona that loaded cleanly.</param>
    /// <param name="rejected">Every file that did not become a Persona.</param>
    /// <param name="teams">The distinct Team names named by any entry.</param>
    internal PersonaIndex(IReadOnlyList<PersonaEntry> entries, IReadOnlyList<RejectedPersonaFile> rejected, IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(rejected);
        ArgumentNullException.ThrowIfNull(teams);

        this.Entries = entries;
        this.Rejected = rejected;
        this.Teams = teams;

        this.byName = entries.ToFrozenDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        this.byAlias = entries.ToFrozenDictionary(entry => entry.Alias, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every Persona that loaded cleanly, ordered by <see cref="PersonaEntry.Name"/> (ordinal).</summary>
    public IReadOnlyList<PersonaEntry> Entries { get; }

    /// <summary>Every file that did not become a Persona, ordered by path (ordinal), for the UI to explain.</summary>
    public IReadOnlyList<RejectedPersonaFile> Rejected { get; }

    /// <summary>The distinct Team names named by any valid Persona's <see cref="PersonaIdentity.Teams"/>, sorted (ordinal).</summary>
    public IReadOnlyList<string> Teams { get; }

    /// <summary>Looks up a Persona by its exact Name, case-insensitively. Returns <see langword="null"/> if none matches.</summary>
    /// <param name="name">The Name to look up.</param>
    public PersonaEntry? ByName(string name) => this.byName.GetValueOrDefault(name);

    /// <summary>
    /// Looks up a Persona by Name first, then by Alias, both case-insensitively. A Name match always
    /// wins, mirroring <see cref="Build"/>'s own collision rule that a file's Alias may never equal a
    /// DIFFERENT file's Name - so in any index actually produced by <see cref="Build"/>, this
    /// ambiguity cannot arise from two distinct live entries; the ordering matters for a hand-built
    /// index such as a unit test constructs directly.
    /// </summary>
    /// <param name="nameOrAlias">The Name or Alias to look up.</param>
    public PersonaEntry? ByNameOrAlias(string nameOrAlias) => this.ByName(nameOrAlias) ?? this.byAlias.GetValueOrDefault(nameOrAlias);

    /// <summary>
    /// Builds an index from an in-memory list of Persona files, doing no filesystem access itself.
    /// Files are processed in path order (ordinal) first, purely so a collision's error text always
    /// names the same "other" path regardless of the caller's enumeration order.
    /// </summary>
    /// <param name="files">Every candidate Persona file's path and full raw text.</param>
    public static PersonaIndex Build(IReadOnlyList<(string Path, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var ordered = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToList();
        var reasons = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var parsed = new List<(string Path, string Text, PersonaIdentity Identity)>();

        foreach (var file in ordered)
        {
            if (PersonaFrontmatter.TryReadIdentity(file.Text, out var identity, out var error))
            {
                parsed.Add((file.Path, file.Text, identity));
            }
            else
            {
                AddReason(reasons, file.Path, error);
            }
        }

        var byNameGroups = parsed
            .GroupBy(file => file.Identity.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        RejectDuplicates(parsed, reasons, "Name", file => file.Identity.Name);
        RejectDuplicates(parsed, reasons, "Alias", file => file.Identity.Alias);
        RejectAliasMatchingAnotherFilesName(parsed, byNameGroups, reasons);

        var entries = parsed
            .Where(file => !reasons.ContainsKey(file.Path))
            .Select(file => new PersonaEntry(file.Identity.Name, file.Identity.Title, file.Identity.Alias, file.Identity.Teams, file.Path, file.Text))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();

        var rejected = reasons
            .Select(pair => new RejectedPersonaFile(pair.Key, string.Join(' ', pair.Value)))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();

        var teams = entries
            .SelectMany(entry => entry.Teams)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(team => team, StringComparer.Ordinal)
            .ToList();

        return new PersonaIndex(entries, rejected, teams);
    }

    /// <summary>Appends one more reason to the growing rejection text for a path, creating the entry on first use.</summary>
    private static void AddReason(Dictionary<string, List<string>> reasons, string path, string reason)
    {
        if (!reasons.TryGetValue(path, out var list))
        {
            list = [];
            reasons[path] = list;
        }

        list.Add(reason);
    }

    /// <summary>
    /// Rejects every file in any group of two or more that share the same value for
    /// <paramref name="keySelector"/> (Name or Alias, compared case-insensitively), naming the
    /// group's OTHER path(s) in each rejected file's reason.
    /// </summary>
    private static void RejectDuplicates(
        IReadOnlyList<(string Path, string Text, PersonaIdentity Identity)> parsed,
        Dictionary<string, List<string>> reasons,
        string fieldName,
        Func<(string Path, string Text, PersonaIdentity Identity), string> keySelector)
    {
        var groups = parsed
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            var files = group.ToList();
            foreach (var file in files)
            {
                var others = files.Where(other => !string.Equals(other.Path, file.Path, StringComparison.Ordinal)).Select(other => other.Path);
                AddReason(reasons, file.Path, $"Persona {fieldName} '{keySelector(file)}' is also used by {FormatPaths(others)}.");
            }
        }
    }

    /// <summary>
    /// Rejects a file whose Alias equals a DIFFERENT file's Name (both sides), and does nothing for a
    /// file whose Alias equals its OWN Name - a plausible authoring choice, not a typo.
    /// </summary>
    private static void RejectAliasMatchingAnotherFilesName(
        IReadOnlyList<(string Path, string Text, PersonaIdentity Identity)> parsed,
        Dictionary<string, List<(string Path, string Text, PersonaIdentity Identity)>> byNameGroups,
        Dictionary<string, List<string>> reasons)
    {
        foreach (var file in parsed)
        {
            if (!byNameGroups.TryGetValue(file.Identity.Alias, out var nameMatches))
            {
                continue;
            }

            var others = nameMatches.Where(match => !string.Equals(match.Path, file.Path, StringComparison.Ordinal)).ToList();
            if (others.Count == 0)
            {
                continue;
            }

            AddReason(reasons, file.Path, $"Persona Alias '{file.Identity.Alias}' matches the Name used by {FormatPaths(others.Select(other => other.Path))}.");

            foreach (var other in others)
            {
                AddReason(reasons, other.Path, $"Persona Name '{other.Identity.Name}' matches the Alias used by '{file.Path}'.");
            }
        }
    }

    /// <summary>Formats a set of paths as a single-quoted, comma-separated list for a rejection reason.</summary>
    private static string FormatPaths(IEnumerable<string> paths) => string.Join(", ", paths.Select(path => $"'{path}'"));
}
