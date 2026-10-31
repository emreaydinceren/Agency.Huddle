namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>What a Team Memory Verdict asks for (Team Memory Helper spec §6.5). There is no delete: Agents delete only when the Human asks.</summary>
internal enum TeamMemoryAction
{
    /// <summary>File nothing.</summary>
    None,

    /// <summary>Create a new topic file.</summary>
    Create,

    /// <summary>Replace the body of a file the Helper was shown.</summary>
    Update,
}
