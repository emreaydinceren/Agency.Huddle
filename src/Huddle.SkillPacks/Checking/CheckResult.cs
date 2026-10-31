namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>Everything <c>Checks.Run</c> returns.</summary>
/// <param name="Findings">Every finding, loading findings included, in the deterministic order of §6.3.</param>
/// <param name="Rows">One entry per row checked.</param>
/// <param name="States">Keyed <c>pack:&lt;id&gt;</c>, <c>catalog:&lt;id&gt;</c>, <c>skill:&lt;name&gt;</c> or, for a file too
/// broken to read an id from, <c>row:pack:&lt;index&gt;</c> / <c>row:catalog:&lt;index&gt;</c>. The only place states are computed.</param>
/// <param name="Summary">The counts of the summary line.</param>
public sealed record CheckResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<RowCheck> Rows,
    IReadOnlyDictionary<string, ItemCheckState> States,
    CheckSummary Summary)
{
    /// <summary>A result with nothing in it.</summary>
    public static CheckResult Empty { get; } = new([], [], new Dictionary<string, ItemCheckState>(StringComparer.Ordinal), new CheckSummary(0, 0, 0, 0, 0));

    /// <summary>The state stored under <paramref name="key"/>, or <see langword="null"/> when there is none.</summary>
    public ItemCheckState? StateOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return this.States.TryGetValue(key, out ItemCheckState state) ? state : null;
    }
}
