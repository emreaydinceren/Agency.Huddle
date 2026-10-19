namespace Agency.Huddle.App.Sidebar;

/// <summary>One Task or Library file a Room's Messages mention.</summary>
/// <param name="Key">The Task id or the file path, as written.</param>
/// <param name="Label">What to show: the snapshot's Label, or the key (Task) or file name (file) when scanned.</param>
/// <param name="LatestMessageId">The id of the newest Message that mentions the key.</param>
/// <param name="Count">The number of Messages (not occurrences) that mention the key.</param>
internal sealed record RoomMention(string Key, string Label, string LatestMessageId, int Count);
