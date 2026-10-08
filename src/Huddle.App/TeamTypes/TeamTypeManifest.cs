using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>Where a Team works: a source repository, a folder, or nothing.</summary>
internal enum WorkspaceKind
{
    /// <summary>A source repository.</summary>
    Repo,

    /// <summary>A plain folder.</summary>
    Folder,

    /// <summary>No workspace.</summary>
    None,
}

/// <summary>The model strength a role asks for.</summary>
internal enum ModelTier
{
    /// <summary>The strongest available model.</summary>
    Strongest,

    /// <summary>A balanced model. The default when a manifest omits the tier.</summary>
    Balanced,

    /// <summary>A fast, low-cost model.</summary>
    Fast,
}

/// <summary>The workspace a team type needs.</summary>
/// <param name="Kind">The workspace kind.</param>
/// <param name="Label">The label shown to the user.</param>
/// <param name="Required">Whether a Team must have a workspace before it starts.</param>
internal sealed record WorkspaceSpec(WorkspaceKind Kind, string Label, bool Required);

/// <summary>A named preset: a set of roles hired together.</summary>
/// <param name="Id">The preset id.</param>
/// <param name="Label">The label shown to the user.</param>
/// <param name="Roles">The role ids in the preset, or null when the manifest omits them.</param>
internal sealed record PresetSpec(string Id, string Label, IReadOnlyList<string>? Roles = null)
{
    /// <summary>The role ids in the preset; empty when the manifest omits them.</summary>
    public IReadOnlyList<string> Roles { get; init; } = Roles ?? [];
}

/// <summary>A process document a team type offers.</summary>
/// <param name="Id">The process id.</param>
/// <param name="File">The file under the type's processes folder.</param>
/// <param name="Default">Whether this process is used when none is chosen.</param>
internal sealed record ProcessRef(string Id, string File, bool Default = false);

/// <summary>The model a role asks for.</summary>
/// <param name="Tier">The model strength; Balanced when the manifest omits it.</param>
/// <param name="DifferentVendorFrom">The role ids whose model vendor this role must differ from, or null when omitted.</param>
internal sealed record ModelSpec(ModelTier Tier = ModelTier.Balanced, IReadOnlyList<string>? DifferentVendorFrom = null)
{
    /// <summary>The role ids whose model vendor this role must differ from; empty when omitted.</summary>
    public IReadOnlyList<string> DifferentVendorFrom { get; init; } = DifferentVendorFrom ?? [];
}

/// <summary>A skill a role carries.</summary>
/// <param name="Name">The skill name.</param>
/// <param name="Source">Either <c>builtin</c> or <c>owner/repo</c>.</param>
/// <param name="Path">The path inside the source, or null for the default location.</param>
internal sealed record SkillRef(string Name, string Source, string? Path);

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
    IReadOnlyList<string>? SuggestedNames = null)
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

/// <summary>A parsed <c>team-type.json</c> manifest (Spec §TeamType).</summary>
/// <param name="SchemaVersion">The manifest format version.</param>
/// <param name="Id">The type id, which is also its folder name.</param>
/// <param name="Version">The semver version of the type.</param>
/// <param name="DisplayName">The dropdown label.</param>
/// <param name="Order">The position in the dropdown.</param>
/// <param name="Workspace">The workspace the type needs.</param>
/// <param name="Coverage">The steps the team check bar requires, or null when omitted.</param>
/// <param name="Presets">The presets the type offers, or null when omitted.</param>
/// <param name="Processes">The process documents the type offers, or null when omitted.</param>
/// <param name="Roles">The roles the type defines, or null when omitted.</param>
/// <param name="RetroRole">The role id a Retro agent is hired as, or null.</param>
internal sealed record TeamTypeManifest(
    int SchemaVersion,
    string Id,
    string Version,
    string DisplayName,
    int Order,
    WorkspaceSpec Workspace,
    IReadOnlyList<string>? Coverage = null,
    IReadOnlyList<PresetSpec>? Presets = null,
    IReadOnlyList<ProcessRef>? Processes = null,
    IReadOnlyList<RoleSpec>? Roles = null,
    string? RetroRole = null)
{
    /// <summary>The steps the team check bar requires; empty when omitted.</summary>
    public IReadOnlyList<string> Coverage { get; init; } = Coverage ?? [];

    /// <summary>The presets the type offers; empty when omitted.</summary>
    public IReadOnlyList<PresetSpec> Presets { get; init; } = Presets ?? [];

    /// <summary>The process documents the type offers; empty when omitted.</summary>
    public IReadOnlyList<ProcessRef> Processes { get; init; } = Processes ?? [];

    /// <summary>The roles the type defines; empty when omitted.</summary>
    public IReadOnlyList<RoleSpec> Roles { get; init; } = Roles ?? [];
}

/// <summary>The one set of JSON options that reads and writes <c>team-type.json</c>.</summary>
internal static class TeamTypeJson
{
    /// <summary>The options for reading manifests: camelCase names, lower-case enum names, comments and trailing commas.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
