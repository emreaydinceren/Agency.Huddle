namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>What the move did, shown once as a snackbar and kept in <c>skill-sources-move.json</c>.</summary>
/// <param name="At">When it ran.</param>
/// <param name="Failed">True when it could not finish; it runs again at the next start.</param>
/// <param name="Catalogs">Catalog files written.</param>
/// <param name="CatalogSkills">Skills listed across them.</param>
/// <param name="OmittedDuplicates">Entries left out because another catalog kept the name.</param>
/// <param name="ShippedPackRows">Rows written for the two Packs Huddle used to ship.</param>
/// <param name="UserPacksMoved">The Human's Packs moved from <c>SkillPacks/</c>.</param>
/// <param name="SkillsPointedAt">Imported Skills copied to <c>fetched/</c> and pointed at.</param>
/// <param name="SkillsLeftInPlace">Imported Skills left only in <c>Skills/</c>.</param>
/// <param name="SkillsWithIgnoredTools">Imported Skills whose <c>tools:</c> line Huddle now ignores.</param>
/// <param name="Problems">What could not be moved.</param>
internal sealed record MoveReport(
    DateTimeOffset At,
    bool Failed,
    int Catalogs,
    int CatalogSkills,
    int OmittedDuplicates,
    int ShippedPackRows,
    int UserPacksMoved,
    int SkillsPointedAt,
    int SkillsLeftInPlace,
    IReadOnlyList<string> SkillsWithIgnoredTools,
    IReadOnlyList<MoveProblem> Problems)
{
    /// <summary>A report for a move that could not finish.</summary>
    public static MoveReport Failure(DateTimeOffset at, IReadOnlyList<MoveProblem> problems)
    {
        return new MoveReport(at, true, 0, 0, 0, 0, 0, 0, 0, [], problems);
    }

    /// <summary>The snackbar text for this report (§6.13, the report table).</summary>
    public string SnackbarText()
    {
        if (this.Failed)
        {
            return "Huddle could not move Packs and Skills to SKILLS.md. It will try again next start.";
        }

        if (this.Problems.Count > 0)
        {
            return $"Packs and Skills moved to SKILLS.md · {this.Problems.Count} could not be moved";
        }

        return this.SkillsWithIgnoredTools.Count > 0
            ? $"Packs and Skills moved to SKILLS.md · {this.SkillsWithIgnoredTools.Count} Skills no longer grant tools"
            : "Packs and Skills moved to SKILLS.md";
    }
}
