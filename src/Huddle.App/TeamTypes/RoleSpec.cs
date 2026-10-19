namespace Agency.Huddle.App.TeamTypes;

/// <summary>One role a team type defines.</summary>
/// <param name="Id">The role id.</param>
/// <param name="Title">The title shown on the card.</param>
/// <param name="Candidate">The path to the candidate Markdown file.</param>
/// <param name="Manager">Whether this is the team's manager role.</param>
/// <param name="Required">Whether Start Team needs this role.</param>
/// <param name="MaxPerTeam">The most Teammates of this role a team may hold; 0 means unlimited; 1 when omitted.</param>
/// <param name="Covers">The coverage steps this role provides, or null when omitted.</param>
/// <param name="Model">The model the role asks for, or null when omitted (Balanced).</param>
/// <param name="Skills">The skills the role carries, or null when omitted.</param>
/// <param name="Packs">The suggested skill pack ids, or null when omitted.</param>
/// <param name="Requires">The connectors the role needs, such as <c>email:read</c>, or null when omitted.</param>
/// <param name="SuggestedNames">The names offered when the role is hired, or null when omitted.</param>
/// <param name="Job">The id of the hiring-pool job this role is; when set, title, persona, skills, model tier, connectors and names come from the pool job.</param>
/// <param name="Pooled">True for a role the catalog adds from the pool that the type does not list: hireable with defaults, never required.</param>
internal sealed record RoleSpec(
    string Id,
    string Title,
    string Candidate,
    bool Manager = false,
    bool Required = false,
    int MaxPerTeam = 1,
    IReadOnlyList<string>? Covers = null,
    ModelSpec? Model = null,
    IReadOnlyList<SkillRef>? Skills = null,
    IReadOnlyList<string>? Packs = null,
    IReadOnlyList<string>? Requires = null,
    IReadOnlyList<string>? SuggestedNames = null,
    string? Job = null,
    bool Pooled = false)
{
    /// <summary>The coverage steps this role provides; empty when omitted.</summary>
    public IReadOnlyList<string> Covers { get; init; } = Covers ?? [];

    /// <summary>The model the role asks for; Balanced when omitted.</summary>
    public ModelSpec Model { get; init; } = Model ?? new ModelSpec();

    /// <summary>The skills the role carries; empty when omitted.</summary>
    public IReadOnlyList<SkillRef> Skills { get; init; } = Skills ?? [];

    /// <summary>The suggested skill pack ids; empty when omitted.</summary>
    public IReadOnlyList<string> Packs { get; init; } = Packs ?? [];

    /// <summary>The connectors the role needs; empty when omitted.</summary>
    public IReadOnlyList<string> Requires { get; init; } = Requires ?? [];

    /// <summary>The names offered when the role is hired; empty when omitted.</summary>
    public IReadOnlyList<string> SuggestedNames { get; init; } = SuggestedNames ?? [];
}
