namespace Agency.Huddle.App.Teams;

/// <summary>How far a journalled rename has got.</summary>
public enum TeamRenameState
{
    /// <summary>Begun; the folder move may not have happened yet.</summary>
    Pending,

    /// <summary>Finished; the old name now resolves to the new one.</summary>
    Done,
}
