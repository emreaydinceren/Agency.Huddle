using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.HiringPool;

/// <summary>One job a Human can hire from the pool, whatever team type they are setting up.</summary>
/// <param name="Id">The job id, lower-case letters, digits and hyphens; it is also the role id the job takes in a type.</param>
/// <param name="Title">The title shown on the card.</param>
/// <param name="Description">One sentence on the job.</param>
/// <param name="Tags">The tags the second cloud filters by, or null when omitted.</param>
/// <param name="Skills">The suggested skill names the second cloud filters by, or null when omitted.</param>
/// <param name="SkillRefs">The installable skills a hire of this job carries, or null when omitted.</param>
/// <param name="TeamTypes">The ids of the team types this job suits, or null when omitted.</param>
/// <param name="Persona">The persona file below the pool folder; <c>personas/{id}.md</c> when omitted.</param>
/// <param name="Model">The model the job asks for, or null when omitted (Balanced).</param>
/// <param name="Requires">The connectors the job needs, such as <c>email:read</c>, or null when omitted.</param>
/// <param name="SuggestedNames">The names offered when the job is hired, or null when omitted.</param>
/// <param name="Packs">The suggested skill pack ids, or null when omitted.</param>
/// <param name="Avatars">The avatar library files that suit the job, best first, or null when omitted.</param>
internal sealed record PoolJob(
    string Id,
    string Title,
    string? Description = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Skills = null,
    IReadOnlyList<SkillRef>? SkillRefs = null,
    IReadOnlyList<string>? TeamTypes = null,
    string? Persona = null,
    ModelSpec? Model = null,
    IReadOnlyList<string>? Requires = null,
    IReadOnlyList<string>? SuggestedNames = null,
    IReadOnlyList<string>? Packs = null,
    IReadOnlyList<string>? Avatars = null)
{
    /// <summary>One sentence on the job; empty when omitted.</summary>
    public string Description { get; init; } = Description ?? string.Empty;

    /// <summary>The tags the second cloud filters by; empty when omitted.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Tags ?? [];

    /// <summary>The suggested skill names the second cloud filters by; empty when omitted.</summary>
    public IReadOnlyList<string> Skills { get; init; } = Skills ?? [];

    /// <summary>The installable skills a hire carries; empty when omitted.</summary>
    public IReadOnlyList<SkillRef> SkillRefs { get; init; } = SkillRefs ?? [];

    /// <summary>The ids of the team types this job suits; empty when omitted.</summary>
    public IReadOnlyList<string> TeamTypes { get; init; } = TeamTypes ?? [];

    /// <summary>The persona file below the pool folder.</summary>
    public string Persona { get; init; } = Persona ?? "personas/" + Id + ".md";

    /// <summary>The model the job asks for; Balanced when omitted.</summary>
    public ModelSpec Model { get; init; } = Model ?? new ModelSpec();

    /// <summary>The connectors the job needs; empty when omitted.</summary>
    public IReadOnlyList<string> Requires { get; init; } = Requires ?? [];

    /// <summary>The names offered when the job is hired; empty when omitted.</summary>
    public IReadOnlyList<string> SuggestedNames { get; init; } = SuggestedNames ?? [];

    /// <summary>The suggested skill pack ids; empty when omitted.</summary>
    public IReadOnlyList<string> Packs { get; init; } = Packs ?? [];

    /// <summary>The avatar library files that suit the job, best first; empty when omitted.</summary>
    public IReadOnlyList<string> Avatars { get; init; } = Avatars ?? [];
}
