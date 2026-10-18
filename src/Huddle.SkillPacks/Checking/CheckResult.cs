using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>The state the checks leave an item in. Huddle refines Usable into New, Available, In use, Update
/// available, Deprecated and Withdrawn (§8.3, §8.4).</summary>
public enum ItemCheckState
{
    /// <summary>Passes every error check and is downloaded.</summary>
    Usable,

    /// <summary>The file is fine but a path is empty, leads nowhere, or has no entry for this computer.</summary>
    NotDownloaded,

    /// <summary>An error check failed.</summary>
    Broken,
}

/// <summary>One SKILLS.md row as checked.</summary>
/// <param name="Kind">The table the row is in.</param>
/// <param name="Index">The row's 0-based index in its table.</param>
/// <param name="LocalPath">The path the row resolved to, or the cell text when it resolved to none.</param>
/// <param name="Line">The row's first line in SKILLS.md; 0 for a virtual row.</param>
/// <param name="State">The state the row's file ended in.</param>
/// <param name="Errors">Errors that belong to the row.</param>
/// <param name="Warnings">Warnings that belong to the row.</param>
public sealed record RowCheck(RowKind Kind, int Index, string LocalPath, int Line, ItemCheckState State, int Errors, int Warnings);

/// <summary>The counts of the <c>PACKS-VALIDATE</c> line (§6.4).</summary>
/// <param name="Rows">Rows checked.</param>
/// <param name="Packs">Packs among them.</param>
/// <param name="Skills">Distinct Skill names reached by the rows checked.</param>
/// <param name="Errors">Error findings.</param>
/// <param name="Warnings">Warning findings.</param>
public sealed record CheckSummary(int Rows, int Packs, int Skills, int Errors, int Warnings);

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
