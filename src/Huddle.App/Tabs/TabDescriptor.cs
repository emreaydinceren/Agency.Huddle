namespace Agency.Huddle.App.Tabs;

/// <summary>What a URL means as a Tab: its key, a fallback title and an icon name.</summary>
/// <param name="Key">The identity of the Tab; two URLs with one key are one Tab.</param>
/// <param name="Title">A title good enough until something richer (a Room's name) is known.</param>
/// <param name="Icon">A symbolic icon name.</param>
internal sealed record TabDescriptor(string Key, string Title, string Icon);
