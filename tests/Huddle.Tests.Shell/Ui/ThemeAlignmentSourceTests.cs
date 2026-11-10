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

        Assert.Contains("border-radius: var(--hu-radius-full);", CssText.RuleBody(css, ".mud-chip.tag-chip.task-tag", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("width: 6px;", CssText.RuleBody(css, ".mud-chip.task-tag::before", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.All(HueSelectors, selector => Assert.Contains("background: var(--mud-palette-", CssText.RuleBody(css, selector, RuleMatch.Last), StringComparison.Ordinal)); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// A dense outlined field is 32px with its padding inside, its helper text starts on the field's edge, and a select's
    /// filled triangle is redrawn as a stroked line chevron (G07, LL-E02, LL-S02).
    /// </summary>
    [Fact]
    public void AppCss_FieldFurniture_MatchesTheMockups()
    {
        string css = CssText.AppCss();
        string field = CssText.RuleBody(css, ".mud-input.mud-input-outlined > input.mud-input-root-outlined.mud-input-root-margin-dense,\n.mud-input.mud-input-outlined > div.mud-input-slot.mud-input-root-outlined.mud-input-root-margin-dense", RuleMatch.First);
        string chevron = CssText.RuleBody(css, ".mud-select .mud-input-adornment-end path[d=\"M7 10l5 5 5-5z\"],\n.mud-autocomplete .mud-input-adornment-end path[d=\"M7 10l5 5 5-5z\"]", RuleMatch.Last);

        Assert.Contains("height: 32px;", field, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("box-sizing: border-box;", field, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("margin-inline-start: -8px;", CssText.RuleBody(css, ".mud-input-control-helper-container", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("d: path(\"M7 10l5 5 5-5\");", chevron, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("fill: none;", chevron, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The card surface puts the Library note, each Right sidebar panel and the open Settings section on a raised card
    /// with the card shadow, and the note's sheet stays 16px in from a narrow pane (FL-01, FL-02, FL-07, FD-01).
    /// </summary>
    [Fact]
    public void AppCss_CardSurface_PutsNoteSettingsAndPanelsOnCards()
    {
        string css = CssText.AppCss();
        string cards = CssText.RuleBody(css, ":root:has([data-hu-surface=\"card\"]) .library-rendered,\n:root:has([data-hu-surface=\"card\"]) .right-sidebar .sidebar-panel,\n:root:has([data-hu-surface=\"card\"]) .settings-section", RuleMatch.Last);

        Assert.Contains("background: var(--hu-ground-raised);", cards, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("box-shadow: var(--hu-shadow-card);", cards, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("width: min(calc(100% - 32px),", CssText.RuleBody(css, ":root:has([data-hu-surface=\"card\"]) .library-rendered", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The navigation panel's Needs you block is flat rows on the sidebar's ground, not a bordered card (G03).</summary>
    [Fact]
    public void AppCss_NeedsYouBlock_IsFlat()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".needs-you-card.mud-paper-outlined", RuleMatch.Last);

        Assert.Contains("border-color: transparent;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("background-color: transparent;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The Tags panel tree starts every label on one line (no centred 64px minimum) with no extra row padding; the Tasks
    /// counts are one dot-separated secondary line; the List | Kanban toggle is a light 24px control (LL-L03, LL-T02).
    /// </summary>
    [Fact]
    public void AppCss_TagsTreeCountsAndKindToggle_AreLight()
    {
        string css = CssText.AppCss();
        string tagRow = CssText.RuleBody(css, ".tags-tree .tags-tree-row.mud-button-root", RuleMatch.Last);

        Assert.Contains("min-width: 0;", tagRow, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("justify-content: flex-start;", tagRow, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("padding-block: 0;", CssText.RuleBody(css, ".tags-tree .mud-treeview-item-content", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("content: \"·\";", CssText.RuleBody(css, ".tasks-counts > * + *::before", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("font-weight: 500;", CssText.RuleBody(css, ".tasks-counts strong", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("height: 24px;", CssText.RuleBody(css, ".huddle-segmented-control.task-toolbar-kind .huddle-segmented-group .mud-toggle-item", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("top: 2px;", CssText.RuleBody(css, ".huddle-segmented-control.task-toolbar-kind .huddle-segmented-indicator", RuleMatch.Last), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
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
