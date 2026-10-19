namespace Agency.Huddle.App.TeamTypes;

/// <summary>A parsed <c>team-type.json</c> manifest (Spec §TeamType).</summary>
/// <param name="SchemaVersion">The manifest format version.</param>
/// <param name="Id">The type id, which is also its folder name.</param>
/// <param name="Version">The semver version of the type.</param>
/// <param name="DisplayName">The dropdown label.</param>
/// <param name="Order">The position in the dropdown.</param>
/// <param name="Workspace">The workspace the type needs.</param>
/// <param name="Coverage">The steps the team check bar requires, or null when omitted.</param>
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
    IReadOnlyList<ProcessRef>? Processes = null,
    IReadOnlyList<RoleSpec>? Roles = null,
    string? RetroRole = null)
{
    /// <summary>The one-line description of the type, filled from the hiring pool; null when the pool has none.</summary>
    public string? Description { get; init; }

    /// <summary>The steps the team check bar requires; empty when omitted.</summary>
    public IReadOnlyList<string> Coverage { get; init; } = Coverage ?? [];

    /// <summary>The process documents the type offers; empty when omitted.</summary>
    public IReadOnlyList<ProcessRef> Processes { get; init; } = Processes ?? [];

    /// <summary>The roles the type defines; empty when omitted.</summary>
    public IReadOnlyList<RoleSpec> Roles { get; init; } = Roles ?? [];
}
