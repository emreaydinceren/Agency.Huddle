namespace Agency.Huddle.App.TeamTypes;

/// <summary>
/// The Blank Team: a choice on the Build a team page that is not a team type file. It has no roles, no process and an
/// optional folder, so the Human decides everything. It lives in code on purpose and is never listed by
/// <see cref="TeamTypeCatalog"/>, so no user folder can replace it and no hiring pool has to name it.
/// </summary>
internal static class BlankTeamType
{
    /// <summary>The id the page keeps in its address (<c>?type=blank</c>).</summary>
    public const string Id = "blank";

    /// <summary>The manifest the page treats like any chosen type: no roles, no processes, an optional folder.</summary>
    public static TeamTypeManifest Manifest { get; } = new(
        SchemaVersion: 1,
        Id: Id,
        Version: "1.0.0",
        DisplayName: "Blank Team",
        Order: 0,
        Workspace: new WorkspaceSpec(WorkspaceKind.Folder, "Folder", Required: false))
    {
        Description = "Start from nothing. You choose the roles, the process and the folder yourself.",
    };
}
