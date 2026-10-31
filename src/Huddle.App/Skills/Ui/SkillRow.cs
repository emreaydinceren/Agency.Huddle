using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>One row of the Skills tab (Skill Packs spec §6.9.3): a <see cref="SkillStore"/> Skill and/or a registry
/// <see cref="SkillEntry"/>, joined by name.</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Copy">The copy in <c>Skills/</c> (or the built-in), when there is one.</param>
/// <param name="Entry">The registry's entry, when a catalog or Pack lists it.</param>
/// <param name="Origin">Which of the three it is.</param>
/// <param name="State">The registry's display state; null for a built-in or Yours Skill, which carries none.</param>
/// <param name="Holders">The Agents whose <c>skills:</c> holds it.</param>
/// <param name="Hidden">Whether <c>hidden.json</c> hides it.</param>
/// <param name="IsNew">Whether its key is new since the Human last left this tab (the registry's display state says so).</param>
internal sealed record SkillRow(
    string Name,
    Skill? Copy,
    SkillEntry? Entry,
    SkillRowOrigin Origin,
    ItemState? State,
    IReadOnlyList<string> Holders,
    bool Hidden,
    bool IsNew)
{
    /// <summary>The key <c>MarkSeenAsync</c> takes for this row.</summary>
    public string SeenKey => $"skill:{this.Name}";

    /// <summary>The description: the copy's, else the registry's.</summary>
    public string Description => this.Copy?.Description is { Length: > 0 } own ? own : this.Entry?.Description ?? string.Empty;

    /// <summary>Joins the Skill store and the registry by name, sorted by name (ordinal, ignoring case).</summary>
    /// <param name="copies">Every Skill the store resolves.</param>
    /// <param name="entries">Every registry Skill.</param>
    /// <param name="stateOf">The registry's display state of an entry.</param>
    /// <param name="holdersOf">The holders of a Skill name.</param>
    /// <param name="hidden">The hidden Skill names.</param>
    public static IReadOnlyList<SkillRow> Join(
        IReadOnlyList<Skill> copies,
        IReadOnlyList<SkillEntry> entries,
        Func<SkillEntry, ItemState> stateOf,
        Func<string, IReadOnlyList<string>> holdersOf,
        IReadOnlySet<string> hidden)
    {
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(stateOf);
        ArgumentNullException.ThrowIfNull(holdersOf);
        ArgumentNullException.ThrowIfNull(hidden);

        Dictionary<string, SkillEntry> byName = new(StringComparer.Ordinal);
        foreach (SkillEntry entry in entries)
        {
            byName.TryAdd(entry.Name, entry);
        }

        List<SkillRow> rows = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (Skill copy in copies)
        {
            seen.Add(copy.Name);
            SkillEntry? entry = copy.Source == SkillSource.Yours ? byName.GetValueOrDefault(copy.Name) : null;
            SkillRowOrigin origin = copy.Source switch
            {
                SkillSource.Default or SkillSource.Overridden => SkillRowOrigin.BuiltIn,
                _ when copy.Provenance is not null || entry is not null => SkillRowOrigin.Registry,
                _ => SkillRowOrigin.Yours,
            };
            ItemState? state = origin == SkillRowOrigin.Registry && entry is not null ? stateOf(entry) : null;
            rows.Add(new SkillRow(copy.Name, copy, entry, origin, state, holdersOf(copy.Name), hidden.Contains(copy.Name), state == ItemState.New));
        }

        foreach (SkillEntry entry in byName.Values.Where(entry => !seen.Contains(entry.Name)))
        {
            ItemState state = stateOf(entry);
            rows.Add(new SkillRow(entry.Name, null, entry, SkillRowOrigin.Registry, state, holdersOf(entry.Name), hidden.Contains(entry.Name), state == ItemState.New));
        }

        return [.. rows.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
