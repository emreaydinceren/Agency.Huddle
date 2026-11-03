namespace Agency.Huddle.App.Teams;

/// <summary>What a Team's workspace path is at session start (Spec §6.4): a repository, a plain folder, or not readable.</summary>
internal enum WorkspaceState
{
    /// <summary>A readable folder with a <c>.git</c> folder or file at its root.</summary>
    Repository,

    /// <summary>A readable folder without <c>.git</c> at its root.</summary>
    Folder,

    /// <summary>A path that is missing, is not a folder, or cannot be listed.</summary>
    Unreadable,
}
