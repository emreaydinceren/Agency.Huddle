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
