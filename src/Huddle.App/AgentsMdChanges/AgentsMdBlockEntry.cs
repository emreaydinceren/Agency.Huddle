namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>One file's part of a Turn's change-notice block (AgentsMdChanges Spec §6.6). A closed hierarchy: only the cases below exist.</summary>
internal abstract record AgentsMdBlockEntry
{
    private protected AgentsMdBlockEntry()
    {
    }

    /// <summary>A file the Agent already follows and whose rules changed.</summary>
    /// <param name="DisplayPath"><c>Teams/</c> plus the ledger's relative path.</param>
    /// <param name="Path">The full path.</param>
    /// <param name="Revisions">The settled edits the Agent has not heard, oldest first.</param>
    /// <param name="CurrentText">The file's text now, for an Adapter with no file tools.</param>
    internal sealed record Changed(string DisplayPath, string Path, IReadOnlyList<AgentsMdRevision> Revisions, string CurrentText) : AgentsMdBlockEntry;

    /// <summary>A file the Agent did not follow before and now does.</summary>
    /// <param name="DisplayPath"><c>Teams/</c> plus the ledger's relative path.</param>
    /// <param name="Path">The full path.</param>
    /// <param name="Text">The file's text now.</param>
    internal sealed record NowFollow(string DisplayPath, string Path, string Text) : AgentsMdBlockEntry;

    /// <summary>A file that no longer applies to the Agent.</summary>
    /// <param name="DisplayPath"><c>Teams/</c> plus the ledger's relative path.</param>
    internal sealed record NoLonger(string DisplayPath) : AgentsMdBlockEntry;

    /// <summary>A file that changed in a way nothing is known about.</summary>
    /// <param name="DisplayPath"><c>Teams/</c> plus the ledger's relative path.</param>
    /// <param name="Path">The full path.</param>
    /// <param name="Text">The file's text now, for an Adapter with no file tools.</param>
    internal sealed record Unknown(string DisplayPath, string Path, string Text) : AgentsMdBlockEntry;
}
