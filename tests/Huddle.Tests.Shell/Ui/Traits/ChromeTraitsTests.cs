using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source tests for the chrome trait variants that the stylesheets ship beside the feature each one styles: <c>status=pill</c>,
/// <c>labels=upper</c>, <c>nav=tile</c>, <c>surface=paper</c> and <c>voice=book</c>. They read the stylesheet as text:
/// each variant is scoped to the marker attribute the layout renders, reads skin tokens (never a pack token) where the
/// registry has one, and declares no colour literal.
/// </summary>
public sealed partial class ChromeTraitsTests
{
    private static readonly string Css = CssText.StripComments(CssText.AppCss(CssNewlines.AsIs));

    private static readonly string RawCss = CssText.AppCss(CssNewlines.AsIs);

    private static readonly string[] ChromeScopes =
    [
        "[data-hu-status=", "[data-hu-labels=", "[data-hu-nav=", "[data-hu-surface=", "[data-hu-voice=",
    ];

    /// <summary>Every non-default value of the five chrome trait enums, with the trait attribute it renders as.</summary>
    public static TheoryData<string, string> NonDefaultValues()
    {
        TheoryData<string, string> data = [];
        Add(data, "status", typeof(StatusStyle));
        Add(data, "labels", typeof(LabelCase));
        Add(data, "nav", typeof(NavStyle));
        Add(data, "surface", typeof(SurfaceStyle));
        Add(data, "voice", typeof(VoiceStyle));
        return data;
    }

    /// <summary>Every non-default value of a chrome trait has at least one rule scoped to its marker attribute.</summary>
    /// <param name="trait">The trait attribute name without the <c>data-hu-</c> prefix.</param>
    /// <param name="value">The kebab-case value.</param>
    [Theory]
    [MemberData(nameof(NonDefaultValues))]
    public void Traits_EveryValueHasCss(string trait, string value)
    {
        string scope = $":root:has([data-hu-{trait}=\"{value}\"])";

        Assert.Contains(scope, Css, StringComparison.Ordinal);
    }

    /// <summary>No chrome trait rule, and nothing else in <c>app.css</c>, names a pack token (<c>--n-*</c>).</summary>
    [Fact]
    public void Traits_NoPackTokens() =>
        Assert.DoesNotContain("--n-", Css, StringComparison.Ordinal);

    /// <summary>A chrome trait rule adds no <c>!important</c>: the scope's specificity is what wins over the base rule.</summary>
    [Fact]
    public void Traits_AddNoImportant() =>
        Assert.DoesNotContain("!important", CssText.ScopedRules(RawCss, ChromeScopes), StringComparison.Ordinal);

    /// <summary>The surface section carries a sub-header per Nordic section it lifts, so a later Theme can peel parts off.</summary>
    [Fact]
    public void SurfacePaper_HasSubHeaders()
    {
        int subHeaders = SubHeader().Count(RawCss);

        Assert.True(subHeaders >= 20, $"Expected a sub-header per source section, found {subHeaders}.");
    }

    /// <summary>
    /// <c>status=pill</c> draws the status chip as a full pill, bolds the warning one, draws a Task state as a shape dot,
    /// leaves High and Urgent priority in their status chip's tone (the pack's clay recipe never applied), and puts tags on the sunk ground.
    /// </summary>
    [Fact]
    public void StatusPill_PinsShapeDotsPriorityAndTags()
    {
        string chip = Body("status", "pill", ".mud-chip.status-chip");
        string warning = Body("status", "pill", ".mud-chip.status-chip.status-chip-warning");
        string dot = Body("status", "pill", ".mud-chip.task-status-chip::before");
        string tag = Body("status", "pill", ".mud-chip.tag-chip");

        Assert.Contains("border-radius: 999px;", chip, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600;", warning, StringComparison.Ordinal);
        Assert.Contains("width: 7px;", dot, StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
        Assert.Contains("border: 1.5px solid currentColor;", dot, StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
        Assert.Contains("background: var(--hu-ground-sunk);", tag, StringComparison.Ordinal);
        Assert.Contains("color: var(--hu-text-subtle);", tag, StringComparison.Ordinal);
        Assert.DoesNotContain("[data-hu-status=\"pill\"]) .mud-chip.priority-high", Css, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>labels=upper</c> sets the micro-label recipe (overline size, 600, tracking, uppercase, secondary ink) on the
    /// sidebar group heads, the right sidebar titles, the list header cells and the drawer group labels.
    /// </summary>
    [Fact]
    public void LabelsUpper_PinsTheMicroLabelRecipe()
    {
        foreach (string selector in new[] { ".needs-you-heading", ".sidebar-panel-title", ".group-label" })
        {
            string body = Body("labels", "upper", selector);

            Assert.Contains("text-transform: uppercase;", body, StringComparison.Ordinal);
            Assert.Contains("color: var(--mud-palette-text-secondary);", body, StringComparison.Ordinal);
        }

        string head = Body("labels", "upper", ".mud-table.task-list-grid .mud-table-head .mud-table-cell");

        Assert.Contains("text-transform: uppercase;", head, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-ground-page);", head, StringComparison.Ordinal);
        Assert.Contains("color: var(--mud-palette-text-secondary);", Body("labels", "upper", ".task-detail-prop-label"), StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>nav=tile</c> makes the rail buttons 36px tiles with a 3px bar in the current-icon colour, quiets sidebar rows to
    /// the subtle ink, rounds the top-bar buttons, and gives the Library tree's current file the soft primary fill.
    /// </summary>
    [Fact]
    public void NavTile_PinsRailSidebarSettingsAndTree()
    {
        string rail = Body("nav", "tile", ".activity-bar-item.mud-icon-button");
        string bar = Body("nav", "tile", ".activity-bar-item.activity-bar-item-selected::before");
        string row = Body("nav", "tile", ".mud-drawer .mud-navmenu .mud-nav-link");
        string rowHover = Body("nav", "tile", ".mud-drawer .mud-navmenu .mud-nav-link:hover:not(.active)");
        string settings = Body("nav", "tile", ".settings-nav .mud-nav-link");
        string topBar = Body("nav", "tile", ".top-bar-end .mud-icon-button");
        string tree = Body("nav", "tile", ".library-tree .mud-treeview-item-content.mud-treeview-item-selected");

        Assert.Contains("width: 36px;", rail, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-current-icon);", bar, StringComparison.Ordinal);
        Assert.Contains("color: var(--hu-text-subtle);", row, StringComparison.Ordinal);
        Assert.Contains("background-color: var(--hu-hover);", rowHover, StringComparison.Ordinal);
        Assert.Contains("color: var(--hu-text-subtle);", settings, StringComparison.Ordinal);
        Assert.Contains("width: 30px;", topBar, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-tone-primary-soft);", tree, StringComparison.Ordinal);
        Assert.Contains("color: var(--hu-accent-ink);", tree, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>surface=paper</c> shows the room-kind chip and the Tasks-need-you pill that every other Theme hides, reads the
    /// warning tones for the pill and notices, and paints buttons, dialogs, popovers, segmented controls and switches
    /// from the grounds, lines and card shadow tokens.
    /// </summary>
    [Fact]
    public void SurfacePaper_PinsChipsPillButtonsDialogsAndSwitches()
    {
        string kind = Body("surface", "paper", ".room-kind-chip");
        string pill = Body("surface", "paper", ".tasks-need-you-pill");
        string notice = Body("surface", "paper", ".budget-prompt");
        string primary = Body("surface", "paper", ".mud-button-filled.mud-button-filled-primary:hover");
        string dialog = Body("surface", "paper", ".mud-dialog");
        string indicator = Body("surface", "paper", ".huddle-segmented-indicator");
        string track = Body("surface", "paper", ".mud-switch .mud-checked + .mud-switch-track");
        string tooltip = Body("surface", "paper", ".mud-tooltip.mud-tooltip-default");

        Assert.Contains("display: inline-flex;", kind, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--hu-line-control);", kind, StringComparison.Ordinal);
        Assert.Contains("border: 1px solid var(--hu-tone-warning-line);", pill, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-tone-warning-tint);", notice, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-accent-ink);", primary, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-ground-raised);", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("box-shadow", dialog, StringComparison.Ordinal);
        Assert.Contains("box-shadow: var(--hu-shadow-card);", indicator, StringComparison.Ordinal);
        Assert.Contains("background-color: var(--mud-palette-primary);", track, StringComparison.Ordinal);
        Assert.Contains("background: var(--mud-palette-text-primary);", tooltip, StringComparison.Ordinal);
        Assert.Contains("color: var(--hu-ground-page);", tooltip, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>voice=book</c> sets the Library's rendered document in the reading face at a 1.72 line, tints its block quote
    /// from the info tone, rails the outline with the primary edge, and sets the empty-state sentence in italics.
    /// </summary>
    [Fact]
    public void VoiceBook_PinsProseOutlineAndEmptyState()
    {
        string prose = Body("voice", "book", ".library-rendered");
        string quote = Body("voice", "book", ".library-rendered blockquote");
        string outline = Body("voice", "book", ".outline-row.active");
        string empty = Body("voice", "book", ".empty-state .empty-state-text");
        string hero = Body("voice", "book", ".mud-dialog:has(.agent-profile-card-avatar) .mud-dialog-title .mud-typography-h6");

        Assert.Contains("font-family: var(--font-read);", prose, StringComparison.Ordinal);
        Assert.Contains("line-height: 1.72;", prose, StringComparison.Ordinal);
        Assert.Contains("background: var(--hu-tone-info-soft);", quote, StringComparison.Ordinal);
        Assert.Contains("border-left-color: var(--mud-palette-primary);", outline, StringComparison.Ordinal);
        Assert.Contains("font-style: italic;", empty, StringComparison.Ordinal);
        Assert.Contains("font-family: var(--font-read);", hero, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>nav=tint</c> is the default and draws nothing of its own: the rules it once held are Cobalt Soft's. The Task edit page's group labels and the
    /// blocked card's bar live on <c>surface=flat</c>, the value only Cobalt Soft sets; the Task List header-cell rule was dead (<c>labels=upper</c> overrode it) and is gone.
    /// </summary>
    [Fact]
    public void NavTint_HasNoRulesAndSurfaceFlatCarriesThem()
    {
        Assert.DoesNotContain("data-hu-nav=\"tint\"", Css, StringComparison.Ordinal);

        string label = Body("surface", "flat", ".task-edit-page .task-detail-group > .group-label");
        string blocked = Body("surface", "flat", ".task-card-blocked");

        Assert.Contains("text-transform: none;", label, StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
        Assert.Contains("inset 3px 0 0", blocked, StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
    }

    /// <summary>The traits are nine, and neither <c>marks</c> nor <c>reading</c> remains as an axis, a scope or a marker attribute.</summary>
    [Fact]
    public void Traits_AreNineAndMarksAndReadingAreGone()
    {
        Assert.Equal(9, SkinLoader.TraitValues.Count);
        Assert.DoesNotContain("marks", SkinLoader.TraitValues.Keys);
        Assert.DoesNotContain("reading", SkinLoader.TraitValues.Keys);
        Assert.Contains("voice", SkinLoader.TraitValues.Keys);
        Assert.DoesNotContain("data-hu-marks", Css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-hu-reading", Css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-hu-marks", new SkinTraits().ToMarkerAttributes().Keys);
    }

    private static void Add(TheoryData<string, string> data, string trait, Type enumType)
    {
        foreach (string name in Enum.GetNames(enumType).Skip(1))
        {
            data.Add(trait, Kebab(name));
        }
    }

    private static string Kebab(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "-$1", RegexOptions.CultureInvariant).ToLowerInvariant();

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

    [GeneratedRegex(@"^/\* -- surface=", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SubHeader();
}
