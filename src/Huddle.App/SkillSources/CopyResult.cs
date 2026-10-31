namespace Agency.Huddle.App.SkillSources;

/// <summary>The outcome of a batch. The caller changes an Agent file only when <see cref="Ok"/>.</summary>
/// <param name="Copied">The Skills now in <c>Skills/</c>.</param>
/// <param name="Failed">The Skills that are not.</param>
internal sealed record CopyResult(IReadOnlyList<CopiedSkill> Copied, IReadOnlyList<CopyFailure> Failed)
{
    /// <summary>True when every Skill was copied or already current.</summary>
    public bool Ok => this.Failed.Count == 0;
}
