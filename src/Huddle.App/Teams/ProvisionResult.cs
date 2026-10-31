namespace Agency.Huddle.App.Teams;

/// <summary>How a <see cref="ProcessProvisioner"/> call ended.</summary>
internal enum ProvisionResult
{
    /// <summary><c>Process.md</c> was written and <c>_team.md</c> names it.</summary>
    Created,

    /// <summary>The Team already has a <c>Process.md</c>; nothing was changed.</summary>
    AlreadyExists,

    /// <summary>The Team has no readable <c>_team.md</c>, or its team type is unknown; nothing was written.</summary>
    UnknownTeam,

    /// <summary>The team type offers no process with that id, or its file is unreadable; nothing was written.</summary>
    UnknownProcess,

    /// <summary>The file could not be written; nothing was changed.</summary>
    Failed,
}
