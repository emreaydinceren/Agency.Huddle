namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Everything the update card (G7) draws.</summary>
/// <param name="PackId">The Pack id, or the Skill name for a Skill outside a Pack.</param>
/// <param name="Title">The Pack title or the Skill name.</param>
/// <param name="FromVersion">The version the holders record, or empty.</param>
/// <param name="ToVersion">The Pack file's version, or empty.</param>
/// <param name="FileChanges">Skills whose files changed.</param>
/// <param name="Added">Skills the Pack lists that a holder lacks.</param>
/// <param name="Removed">Skills the Pack no longer lists. Always empty: Huddle keeps no earlier copy of a Pack file.</param>
/// <param name="ConsultWhenFrom">The phrase before, when known.</param>
/// <param name="ConsultWhenTo">The Pack's phrase now.</param>
/// <param name="Agents">The holders whose file would change.</param>
internal sealed record PackUpdatePlan(
    string PackId,
    string Title,
    string FromVersion,
    string ToVersion,
    IReadOnlyList<SkillFileChange> FileChanges,
    IReadOnlyList<SkillMembershipChange> Added,
    IReadOnlyList<SkillMembershipChange> Removed,
    string? ConsultWhenFrom,
    string? ConsultWhenTo,
    IReadOnlyList<AgentChange> Agents)
{
    /// <summary>Whether nothing would change.</summary>
    public bool IsEmpty => this.FileChanges.Count == 0 && this.Agents.Count == 0;
}
