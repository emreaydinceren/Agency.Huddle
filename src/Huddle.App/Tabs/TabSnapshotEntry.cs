namespace Agency.Huddle.App.Tabs;

/// <summary>One Tab in a <see cref="TabGroupSnapshot"/>. The key is not stored: it is derived from the URL on restore.</summary>
/// <param name="Id">The Tab id.</param>
/// <param name="Url">The site-relative address.</param>
/// <param name="Title">The label.</param>
/// <param name="Icon">The symbolic icon name.</param>
/// <param name="Preview">Whether the Tab is still a preview.</param>
/// <param name="Pinned">Whether the Tab is pinned; absent in a version 1 snapshot.</param>
internal sealed record TabSnapshotEntry(string Id, string Url, string Title, string Icon, bool Preview, bool Pinned = false);
