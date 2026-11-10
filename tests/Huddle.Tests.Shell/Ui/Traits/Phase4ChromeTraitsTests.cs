using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source tests for the two chrome variants phase 4 adds at the end of <c>app.css</c>: <c>surface=hairline</c> (Lumen)
/// and <c>nav=bar</c> (Fluent). They read the stylesheet as text: each variant is scoped to the marker attribute the
/// layout renders, reads skin tokens where the registry has one and declares no colour literal.
/// </summary>
public sealed partial class Phase4ChromeTraitsTests
{
    private static readonly string RawCss = CssText.AppCss(CssNewlines.AsIs);

    private static readonly string Css = CssText.StripComments(RawCss);

    /// <summary>The comment-stripped rules scoped to <c>surface=hairline</c> or <c>nav=bar</c>, wherever they sit.</summary>
    private static string Phase4 => CssText.ScopedRules(RawCss, "[data-hu-surface=\"hairline\"]", "[data-hu-nav=\"bar\"]");

    /// <summary>The two new values, with the trait attribute each renders as.</summary>
    public static TheoryData<string, string> NewValues() =>
        new()
        {
            { "surface", "hairline" },
            { "nav", "bar" },
        };

    /// <summary>The enums carry the new values, so a skin can name them.</summary>
    [Fact]
    public void Enums_CarryTheNewValues()
    {
        Assert.Equal("Hairline", SurfaceStyle.Hairline.ToString());
        Assert.Equal("Bar", NavStyle.Bar.ToString());
    }

    /// <summary>Each new value has at least one rule scoped to its marker attribute.</summary>
    /// <param name="trait">The trait attribute name without the <c>data-hu-</c> prefix.</param>
    /// <param name="value">The kebab-case value.</param>
    [Theory]
    [MemberData(nameof(NewValues))]
    public void Traits_EveryValueHasCss(string trait, string value) =>
        Assert.Contains($":root:has([data-hu-{trait}=\"{value}\"])", Css, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup

    /// <summary>The rules scoped to the two phase 4 chrome values use no <c>!important</c> and no pack token.</summary>
    [Fact]
    public void ChromeRules_AddNoImportantOrPackTokens()
    {
        Assert.DoesNotContain("!important", Phase4, StringComparison.Ordinal);
        Assert.DoesNotContain("--n-", Phase4, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>surface=hairline</c> shows the room-kind chip and Tasks-need-you pill, draws cards and dialogs on the
    /// region line with no resting shadow at the plain radius plus 2px (a Board lane reads the board.column tokens), and lets floating layers use the panel shadow
    /// in place of a border.
    /// </summary>
    [Fact]
    public void SurfaceHairline_PinsCardsLanesDialogsAndFloatingLayers()
    {
        string kind = Body("surface", "hairline", ".room-kind-chip");
        string pill = Body("surface", "hairline", ".tasks-need-you-pill");
        string card = Body("surface", "hairline", ".task-card");
        string agent = Body("surface", "hairline", ".mud-paper.agent-card");
        string dialog = Body("surface", "hairline", ".mud-dialog");
        string popover = Body("surface", "hairline", ".mud-popover.mud-paper");
        string filled = Body("surface", "hairline", ".mud-button-root.mud-button-filled");
        string segmented = Body("surface", "hairline", ".huddle-segmented-control");
        string notice = Body("surface", "hairline", ".budget-prompt");
        string sidebar = Body("surface", "hairline", ".sidebar-panel + .sidebar-panel");

        Assert.Contains("border: 1px solid var(--hu-line-control);", kind, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border-radius: var(--hu-radius-full);", pill, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("var(--hu-tone-warning-line)", pill, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: none;", card, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border: 1px solid var(--hu-line-region);", card, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: none;", agent, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: var(--hu-shadow-panel);", dialog, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border: 0;", dialog, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border-radius: var(--hu-radius-panel);", dialog, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: var(--hu-shadow-panel);", popover, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border: 0;", popover, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: none;", filled, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border: 1px solid var(--hu-line-region);", segmented, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: inset 3px 0 0 var(--hu-needs);", notice, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border-top: 1px solid var(--hu-line-region);", sidebar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
    }

    /// <summary>
    /// <c>surface=hairline</c> reads the control line for inputs and the search box, and the focus tokens for the
    /// focused field; the switch track takes the control line.
    /// </summary>
    [Fact]
    public void SurfaceHairline_PinsInputsSearchAndSwitch()
    {
        string input = Body("surface", "hairline", ".mud-input.mud-input-outlined .mud-input-outlined-border");
        string focus = Body("surface", "hairline", ".mud-input.mud-input-outlined:focus-within");
        string focusBorder = Body("surface", "hairline", ".mud-input.mud-input-outlined:focus-within .mud-input-outlined-border");
        string search = Body("surface", "hairline", ".search-bar .mud-input.mud-input-outlined");
        string track = Body("surface", "hairline", ".mud-switch .mud-switch-track");

        Assert.Contains("border-color: var(--hu-line-control);", input, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: var(--hu-field-focus-shadow);", focus, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("border-color: var(--hu-field-focus-line);", focusBorder, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background: var(--hu-ground-sunk);", search, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background-color: var(--hu-line-control);", track, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
    }

    /// <summary>
    /// <c>nav=bar</c> marks the current rail item with a 3px accent bar and no fill or shadow, and marks the current
    /// sidebar and Settings row with a raised fill plus a 3px bar inside its left edge.
    /// </summary>
    [Fact]
    public void NavBar_PinsRailAndRows()
    {
        string railCurrent = Body("nav", "bar", ".activity-bar-item.activity-bar-item-selected");
        string railBar = Body("nav", "bar", ".activity-bar-item.activity-bar-item-selected::before");
        string row = Body("nav", "bar", ".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)");
        string rowBar = Body("nav", "bar", ".mud-drawer .mud-navmenu .mud-nav-link.active:not(.mud-nav-link-disabled)::before");
        string settings = Body("nav", "bar", ".settings-nav .settings-nav-item-active .mud-nav-link");
        string topBar = Body("nav", "bar", ".mud-appbar.top-bar");

        Assert.Contains("background-color: transparent;", railCurrent, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: none;", railCurrent, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("color: var(--hu-accent-ink);", railCurrent, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("width: 3px;", railBar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background: var(--hu-accent-ink);", railBar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background-color: var(--hu-current-background);", row, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("box-shadow: var(--hu-current-shadow);", row, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("width: 3px;", rowBar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background: var(--hu-accent-ink);", rowBar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background: var(--hu-current-background);", settings, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
        Assert.Contains("background-color: var(--hu-ground-rail);", topBar, StringComparison.Ordinal); // contains-ok: stylesheet text, not markup
    }

    /// <summary>The merged declaration text of every rule whose selector list holds the scoped <paramref name="selector"/>.</summary>
    private static string Body(string trait, string value, string selector)
    {
        string wanted = $":root:has([data-hu-{trait}=\"{value}\"]) {selector}";
        StringBuilder merged = new();
        foreach (Match rule in Rule().Matches(Css))
        {
            IEnumerable<string> selectors = rule.Groups["sel"].Value
                .Split(',')
                .Select(static part => Whitespace().Replace(part, " ").Trim());
            if (selectors.Contains(wanted, StringComparer.Ordinal))
            {
                merged.Append(Whitespace().Replace(rule.Groups["body"].Value, " ")).Append(' ');
            }
        }

        Assert.True(merged.Length > 0, $"No rule scoped to {trait}={value} for '{selector}'.");
        return merged.ToString();
    }

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Rule();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

}
