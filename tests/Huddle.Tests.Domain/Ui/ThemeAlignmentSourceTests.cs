using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text guards for the cross-Theme rules the side-by-side audit of 2026-10-30 added (tier 2): the Room title,
/// its revealed controls, and a Task tag's outlined pill and dot. A browser is not rendered here, so each value is
/// pinned in the stylesheet that carries it; the running app was compared with the mockups separately.
/// </summary>
public sealed class ThemeAlignmentSourceTests
{
    /// <summary>The Room title is the h6 role at 600 in every Theme, not the page h5 (LL-C01, FL-06, CL-03).</summary>
    [Fact]
    public void AppCss_RoomTitle_IsTheH6RoleAtSemibold()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".room-header .page-header-h1.mud-typography", RuleMatch.Last);

        Assert.Contains("font-size: var(--mud-typography-h6-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("font-weight: 600;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>Rename and Copy link are hidden until the title row is hovered or focused, and always shown without hover.</summary>
    [Fact]
    public void AppCss_RoomTitleControls_AreRevealedOnHoverOrFocus()
    {
        string css = CssText.AppCss();

        Assert.Contains("opacity: 0;", CssText.RuleBody(css, ".room-header .page-header-title .mud-icon-button", RuleMatch.First), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("opacity: 1;", CssText.RuleBody(css, ".room-header .page-header-title:hover .mud-icon-button,\n.room-header .page-header-title:focus-within .mud-icon-button", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("@media (hover: none) {\n    .room-header .page-header-title .mud-icon-button {\n        opacity: 1;", css.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A Task tag is an outlined pill led by a 6px dot, and each of the six hue classes paints that dot (LL-K01, CL-06).</summary>
    [Fact]
    public void AppCss_TaskTag_IsAnOutlinedPillWithAHuedDot()
    {
        string css = CssText.AppCss();

        Assert.Contains("border-radius: 999px;", CssText.RuleBody(css, ".mud-chip.tag-chip.task-tag", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("width: 6px;", CssText.RuleBody(css, ".mud-chip.task-tag::before", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.All(HueSelectors, selector => Assert.Contains("background: var(--mud-palette-", CssText.RuleBody(css, selector, RuleMatch.Last), StringComparison.Ordinal)); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>One dot rule per entry of <c>TaskFieldView.TagClasses</c>.</summary>
    private static readonly string[] HueSelectors =
    [
        ".mud-chip.task-tag-hue-0::before",
        ".mud-chip.task-tag-hue-1::before",
        ".mud-chip.task-tag-hue-2::before",
        ".mud-chip.task-tag-hue-3::before",
        ".mud-chip.task-tag-hue-4::before",
        ".mud-chip.task-tag-hue-5::before",
    ];
}
