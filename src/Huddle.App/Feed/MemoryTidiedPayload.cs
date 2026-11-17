namespace Agency.Huddle.App.Feed;

/// <summary>Memory Tidy changed topic files in one pass (Memory Tidy spec §6.13).</summary>
/// <param name="HelperId">The Helper's id, for the card link in the preview.</param>
/// <param name="Name">The Helper's display name.</param>
/// <param name="Path">The absolute path of the pass's changes.md.</param>
/// <param name="Tidied">Files merged into, rewritten or re-described.</param>
/// <param name="Dropped">Files dropped or mostly emptied.</param>
/// <param name="Folders">Memory folders changed.</param>
/// <param name="Stopped">Whether the pass ended before every folder was looked at.</param>
/// <param name="First">Whether this is the first pass that changed anything on this install.</param>
internal sealed record MemoryTidiedPayload(string HelperId, string Name, string Path, int Tidied, int Dropped, int Folders, bool Stopped, bool First) : FeedPayload;
