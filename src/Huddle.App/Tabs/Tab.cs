namespace Agency.Huddle.App.Tabs;

/// <summary>
/// One open page in the tab strip. A Tab is a remembered URL: the page host mounts the page of the
/// active Tab of each group, so nothing but this record survives while a Tab is in the background
/// (see <c>docs/Huddle.Tabs-Specifications.md</c> section 4).
/// </summary>
/// <param name="Id">Unique among the open Tabs; names the Tab to activate, keep, pin or close.</param>
/// <param name="Key">What makes two URLs the same Tab, derived by <see cref="TabKeys.Describe(string)"/>. Unique across every group.</param>
/// <param name="Url">The site-relative address the Tab shows, including its query.</param>
/// <param name="Title">The label on the Tab.</param>
/// <param name="Icon">A symbolic icon name; the strip maps it to a MudBlazor icon.</param>
/// <param name="IsPreview">Whether the Tab is still a preview, drawn in italics and replaced by the next preview.</param>
/// <param name="IsPinned">Whether the Tab is pinned: kept at the start of its group, drawn as its icon alone, and never a preview.</param>
public sealed record Tab(string Id, string Key, string Url, string Title, string Icon, bool IsPreview, bool IsPinned = false);
