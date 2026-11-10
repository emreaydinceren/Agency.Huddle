using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the round-3 foundation in the stylesheet (visual audit A1, A3, A4, A5, A6, A7, A8, B2, B4g, B8c, B10a-d): the
/// ink ladder is declared once and is the only source of lines and washes, the 1.5x radius survives only on the message
/// bubble and the Composer, and the shared classes the screens adopt exist with their specified geometry.
/// </summary>
public sealed class R3FoundationCssTests
{
    /// <summary>
    /// The five ladder rungs are declared in <c>app-vars.css</c> as the <c>--ink-*</c> skin tokens, each as Ink mixed
    /// into Surface at its percentage (see <c>SkinCssTests</c>).
    /// </summary>
    /// <param name="name">The custom property.</param>
    /// <param name="percent">The share of Ink.</param>
    [Theory]
    [InlineData("--ink-strip", 4)]
    [InlineData("--ink-wash", 6)]
    [InlineData("--ink-rule", 10)]
    [InlineData("--ink-edge", 18)]
    [InlineData("--ink-field", 30)]
    public void AppVars_DeclaresEachInkRung(string name, int percent)
    {
        string vars = CssText.Read(RepoPaths.App("wwwroot", "app-vars.css"));

        Assert.Contains($"{name}: color-mix(in srgb, var(--mud-palette-text-primary) {percent}%, var(--mud-palette-surface));", vars, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>Lines and washes come from the ladder: no rule reads the palette's own Divider, DividerLight, LinesInputs or BackgroundGray, which sit on different rungs in different Themes.</summary>
    [Fact]
    public void AppCss_DrawsLinesAndWashesFromTheLadderNotThePaletteGreys()
    {
        string css = CssText.AppCss();

        Assert.DoesNotMatch(@"var\(--mud-palette-(divider|divider-light|lines-inputs|background-gray)\)", css);
    }

    /// <summary>1.5x the base radius stays on the message bubble, the Composer and its drop hint; every other surface is 1x. A trait variant (a rule scoped to a <c>data-hu-*</c> marker) is a Theme's choice and is not counted.</summary>
    [Fact]
    public void AppCss_OneAndAHalfRadiusSurvivesOnlyOnBubbleAndComposer()
    {
        string css = CssText.AppCss();
        List<string> selectors = [];
        foreach (Match rule in Regex.Matches(css, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)))
        {
            bool traitVariant = CssText.StripComments(rule.Groups["sel"].Value).Trim().StartsWith(":root:has([data-hu-", StringComparison.Ordinal); // contains-ok: a trait variant rule is scoped to its marker attribute
            if (!traitVariant && rule.Groups["body"].Value.Contains("var(--mud-default-borderradius) * 1.5", StringComparison.Ordinal))
            {
                selectors.Add(rule.Groups["sel"].Value.Trim());
            }
        }

        Assert.Equal([".composer-drop-hint", ".composer-input", ".message-bubble"], selectors.Select(static selector => CssText.StripComments(selector).Trim()).Order(StringComparer.Ordinal));
    }

    /// <summary>The Group label is 12px at 600 in Quiet ink, sentence case, 8px above its fields.</summary>
    [Fact]
    public void AppCss_GroupLabel_IsCaptionSizeSemiboldQuietInk()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".group-label");

        Assert.Contains("font-size: var(--mud-typography-caption-size);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("font-weight: 600;", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--mud-palette-text-secondary);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("text-transform: none;", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("margin: 0 0 8px 0;", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The Selected shape is the Accent tint at 14% (20% hovered), accent ink at 500, inset 4px, at three quarters of the base radius.</summary>
    [Fact]
    public void AppCss_SelectedShape_IsTheSelectedTreatment()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".selected-shape");

        Assert.Contains("margin-inline: 4px;", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-radius: calc(var(--mud-default-borderradius) * 0.75);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("var(--hu-selected-background)", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("font-weight: 500;", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("var(--hu-selected-background-hover)", CssText.RuleBody(CssText.AppCss(), ".selected-shape:hover,\n.selected-shape:focus-visible"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>A filter chip is a 28px pill with 0 12px padding and 13px text; unset it is a dashed Field line, set it takes the accent border and tint.</summary>
    [Fact]
    public void AppCss_FilterChip_IsA28pxPillDashedUntilSet()
    {
        string unset = CssText.RuleBody(CssText.AppCss(), ".mud-chip.filter-chip");
        string set = CssText.RuleBody(CssText.AppCss(), ".mud-chip.filter-chip.mud-chip-selected");

        Assert.Contains("height: 28px;", unset, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("padding: 0 12px;", unset, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-radius: 999px;", unset, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("font-size: var(--mud-typography-body2-size);", unset, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border: 1px dashed var(--ink-field);", unset, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border: 1px solid var(--mud-palette-primary);", set, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains(".mud-chip.filter-chip.is-set,", CssText.AppCss(), StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>A compact toolbar field is a 28px box with 13px text.</summary>
    [Fact]
    public void AppCss_ToolbarCompact_IsA28pxBoxWithThirteenPixelText()
    {
        string css = CssText.AppCss();

        Assert.Matches(@"\.mud-input-control\.toolbar-compact \{[^}]*height: 28px;", css);
        Assert.Matches(@"\.toolbar-compact \.mud-input\.mud-input-outlined \.mud-input-root\.mud-input-root-outlined \{[^}]*height: 28px;[^}]*font-size: var\(--mud-typography-body2-size\);", css);
    }

    /// <summary>Dialog chrome is set once: the close is a 28px box with an 18px icon, and the title, content and actions use the 24px insets.</summary>
    [Fact]
    public void AppCss_DialogChrome_IsSetOnce()
    {
        string css = CssText.AppCss();

        Assert.Matches(@"\.mud-dialog \.mud-dialog-title \{\s*padding: 16px 24px 8px;", css);
        Assert.Matches(@"\.mud-dialog \.mud-dialog-content \{\s*padding: 8px 24px;", css);
        Assert.Matches(@"\.mud-dialog \.mud-dialog-actions \{\s*padding: 12px 24px 16px;", css);
        Assert.Matches(@"\.mud-button-close \{[^}]*width: 28px;[^}]*height: 28px;[^}]*color: var\(--mud-palette-action-default\);", css);
        Assert.Matches(@"\.mud-button-close \.mud-icon-root \{\s*font-size: var\(--icon-size-row\);", css);
    }

    /// <summary>A disabled button is Faint ink on a Wash fill, from the Theme, for every disabled button.</summary>
    [Fact]
    public void AppCss_DisabledButton_UsesTheThemesFaintInkOnWash()
    {
        string css = CssText.AppCss();

        Assert.Matches(@"\.mud-button-root\.mud-button:disabled \{\s*color: var\(--mud-palette-text-disabled\) !important;", css);
        Assert.Matches(@"\.mud-button-filled:disabled \{\s*background-color: var\(--ink-wash\) !important;", css);
    }

    /// <summary>An outlined button's border is the Chrome line; table rows are lined on Edge; the tab rail ends in a Chrome line with 24px below it; an outlined input draws the Field line.</summary>
    [Fact]
    public void AppCss_OutlinedButtonTableRowsTabRailAndInputBorder_SitOnTheirRungs()
    {
        string css = CssText.AppCss();

        Assert.Matches(@"\.mud-button-outlined \{\s*border-color: var\(--hu-line-control\);", css);
        Assert.Matches(@"\.mud-table \.mud-table-cell \{\s*border-bottom-color: var\(--ink-edge\);", css);
        Assert.Matches(@"\.mud-tabs-tabbar \{\s*border-bottom: 1px solid var\(--hu-line-region\);", css);
        Assert.Matches(@"\.mud-tabs-panels \{\s*padding-top: 24px;", css);
        Assert.Matches(@"\.mud-input\.mud-input-outlined \.mud-input-outlined-border \{\s*border-color: var\(--ink-field\);", css);
    }

    /// <summary>Links and a selected list row are the accent ink, not raw Primary.</summary>
    [Fact]
    public void AppCss_LinkAndSelectedListItem_AreAccentInk()
    {
        string css = CssText.AppCss();

        Assert.Matches(@"\.mud-link\.mud-primary-text \{\s*color: var\(--hu-accent-ink\) !important;", css);
        Assert.Matches(@"\.mud-list-item\.mud-selected-item \{[^}]*background-color: var\(--hu-selected-background\);", css);
    }
}