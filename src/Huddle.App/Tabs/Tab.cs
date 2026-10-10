namespace Agency.Huddle.App.Tabs;

/// <summary>
/// One open page in the tab strip. A Tab is a remembered URL: the router still mounts the page, so
/// nothing but this record survives while a Tab is in the background (see
/// <c>docs/Huddle.Tabs-Specifications.md</c> section 4).
/// </summary>
/// <param name="Id">Unique among the open Tabs; names the Tab to activate, pin or close.</param>
/// <param name="Key">What makes two URLs the same Tab, derived by <see cref="TabKeys.Describe(string)"/>.</param>
/// <param name="Url">The site-relative address the Tab shows, including its query.</param>
/// <param name="Title">The label on the Tab.</param>
/// <param name="Icon">A symbolic icon name; the strip maps it to a MudBlazor icon.</param>
/// <param name="IsPreview">Whether the Tab is still a preview, drawn in italics and replaced by the next preview.</param>
internal sealed record Tab(string Id, string Key, string Url, string Title, string Icon, bool IsPreview);

/// <summary>What a URL means as a Tab: its key, a fallback title and an icon name.</summary>
/// <param name="Key">The identity of the Tab; two URLs with one key are one Tab.</param>
/// <param name="Title">A title good enough until something richer (a Room's name) is known.</param>
/// <param name="Icon">A symbolic icon name.</param>
internal sealed record TabDescriptor(string Key, string Title, string Icon);
