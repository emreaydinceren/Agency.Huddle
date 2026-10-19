namespace Agency.Huddle.App.Tabs;

/// <summary>One crumb of a page's breadcrumb trail.</summary>
/// <param name="Text">The label shown.</param>
/// <param name="Href">The site-relative address the crumb opens, or <see langword="null"/> for a crumb that is plain text (the current page, or an area with no page of its own).</param>
/// <param name="Icon">A symbolic icon name, as <see cref="TabIcons.For(string)"/> maps it.</param>
public sealed record TrailCrumb(string Text, string? Href, string Icon);