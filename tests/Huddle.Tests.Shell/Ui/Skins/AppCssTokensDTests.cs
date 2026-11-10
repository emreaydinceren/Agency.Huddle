using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Skins;

/// <summary>
/// Phase 2, package D: the Teams, Sidebar, References and Task form stylesheets and the second half of the Tasks stylesheet (from "The Task List at narrower widths") and
/// <c>HuddleSegmentedControl.razor.css</c> read the skin tokens where a token's registered default is the value the
/// site wrote before. A site whose value no token default equals, or that the architecture leaves as a literal recipe,
/// is not pinned here beyond the count of recipes that must remain.
/// </summary>
public sealed class AppCssTokensDTests
{
    private const string RangeStart = "The Task List at narrower widths";

    private const string PrimaryOverSurface14 = "color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))";

    /// <summary>Each hairline between regions reads <c>--hu-line-region</c>: the Task column resizer line and the Room header's closing line.</summary>
    [Theory]
    [InlineData(".task-col-resizer::after", "background: var(--hu-line-region);")]
    [InlineData(".page-header.has-chrome", "border-bottom: 1px solid var(--hu-line-region);")]
    public void RegionLines_ReadTheToken(string selector, string declaration)
    {
        Assert.Contains(declaration, Declarations(selector));
    }

    /// <summary>The Task List's group row is painted <c>--hu-list-group-background</c>, which defaults to the page ground.</summary>
    [Fact]
    public void TaskGroupRow_ReadsListGroupBackground()
    {
        Assert.Contains("background-color: var(--hu-list-group-background);", Declarations(".mud-data-grid.task-list-grid .mud-table-cell.mud-datagrid-group"));
    }

    /// <summary>Cards and panels that are raised over the page read <c>--hu-ground-raised</c>.</summary>
    [Theory]
    [InlineData(".process-step")]
    [InlineData(".team-type-card")]
    [InlineData(".composer-picker")]
    [InlineData(".roster-card")]
    public void RaisedSurfaces_ReadGroundRaised(string selector)
    {
        Assert.Contains("background: var(--hu-ground-raised);", Declarations(selector));
    }

    /// <summary>The global search box fill reads <c>--hu-ground-sunk</c>, in rest, hover and focus.</summary>
    [Fact]
    public void SearchBox_ReadsGroundSunk()
    {
        string selector = ".search-bar .mud-input-control .mud-input.mud-input-outlined,\n"
            + ".search-bar .mud-input-control .mud-input.mud-input-outlined:hover,\n"
            + ".search-bar .mud-input-control .mud-input.mud-input-outlined.mud-focused,\n"
            + ".search-bar .mud-input-control .mud-input.mud-input-outlined:focus-within";

        Assert.Contains("background: var(--hu-ground-sunk);", Declarations(selector));
    }

    /// <summary>A chosen item in a set reads <c>--hu-selected-background</c>, <c>-hover</c> and <c>--hu-accent-ink</c>.</summary>
    [Fact]
    public void SelectedItems_ReadTheSelectedTokens()
    {
        string[] chip =Declarations(".chip-cloud-chip.is-set");
        Assert.Contains("background-color: var(--hu-selected-background);", chip);
        Assert.Contains("color: var(--hu-accent-ink);", chip);

        string[] card =Declarations(".team-type-card-selected");
        Assert.Contains("background: var(--hu-selected-background);", card);
        Assert.Contains("background: var(--hu-selected-background-hover);", Declarations(".team-type-card-selected:hover"));

        string[] pick =Declarations(".composer-picker-item[aria-selected=\"true\"]");
        Assert.Contains("background: var(--hu-selected-background);", pick);
        Assert.Contains("color: var(--hu-accent-ink);", pick);
        Assert.Contains("background: var(--hu-selected-background-hover);", Declarations(".composer-picker-item[aria-selected=\"true\"]:hover"));

        string[] filter =Declarations(".mud-chip.filter-chip.is-set,\n.mud-chip.filter-chip.filter-chip-set,\n.mud-chip.filter-chip.mud-chip-selected");
        Assert.Contains("background-color: var(--hu-selected-background);", filter);
        Assert.Contains("color: var(--hu-accent-ink);", filter);
    }

    /// <summary>
    /// "A filter or control is set" has one token family: the Task toolbar's set chip and the Feed's set Team select read
    /// <c>selected.*</c> and the accent ink, as the filter chips do, and no stylesheet reads a <c>chip.set.*</c> or
    /// <c>control.set.*</c> token (they are deleted).
    /// </summary>
    [Fact]
    public void SetFilters_ReadTheSelectedTokens_NotTheirOwnFamily()
    {
        string css = CssText.AppCss();
        string feedPage = CssText.Read(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "FeedPage.razor.css"));

        string chip = CssText.RuleBody(css, ".task-filter-chip-set");
        Assert.Contains("background-color: var(--hu-selected-background);", chip, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-color: var(--mud-palette-primary);", chip, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--hu-accent-ink);", CssText.RuleBody(css, ".task-filter-chip-set .task-filter-chip-button"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

        string fill = CssText.RuleBodyInList(feedPage, "::deep .feed-team-select-set .mud-input");
        Assert.Contains("background: var(--hu-selected-background);", fill, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--hu-accent-ink);", fill, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-color: var(--mud-palette-primary);", CssText.RuleBody(feedPage, "::deep .feed-team-select-set .mud-input-outlined-border"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

        Assert.DoesNotMatch(@"--hu-(chip|control)-set-", css + feedPage);
    }

    /// <summary>The accent used as text reads <c>--hu-accent-ink</c>.</summary>
    [Theory]
    [InlineData(".build-team-section-number")]
    [InlineData(".team-type-card-selected .team-type-icon")]
    [InlineData(".role-add-card")]
    public void AccentText_ReadsAccentInk(string selector)
    {
        Assert.Contains("color: var(--hu-accent-ink);", Declarations(selector));
    }

    /// <summary>A hovered card or picker row reads <c>--hu-hover</c>.</summary>
    [Theory]
    [InlineData(".team-type-card:hover")]
    [InlineData(".composer-picker-item:hover")]
    public void HoveredRows_ReadHover(string selector)
    {
        Assert.Contains("background: var(--hu-hover);", Declarations(selector));
    }

    /// <summary>The manager card's faint primary tint reads <c>--hu-tone-primary-tint</c>.</summary>
    [Fact]
    public void ManagerRosterCard_ReadsPrimaryTint()
    {
        Assert.Contains("background: var(--hu-tone-primary-tint);", Declarations(".roster-card-manager"));
    }

    /// <summary>The profile avatar's keyboard focus ring reads <c>--hu-focus</c>.</summary>
    [Fact]
    public void ProfileAvatarFocusRing_ReadsFocus()
    {
        Assert.Contains("outline: 2px solid var(--hu-focus);", Declarations(".profile-avatar-button:focus-visible"));
    }

    /// <summary>Every status chip colour except secondary reads its tone token pair, the neutral one reading Quiet text for its ink (there is no <c>tone.neutral.ink</c>: it equalled <c>text.subtle</c> in every Theme); secondary keeps its literal recipe.</summary>
    [Theory]
    [InlineData(".mud-chip.status-chip", "neutral")]
    [InlineData(".mud-chip.status-chip-info", "info")]
    [InlineData(".mud-chip.status-chip-warning", "warning")]
    [InlineData(".mud-chip.status-chip-error", "error")]
    [InlineData(".mud-chip.status-chip-success", "success")]
    [InlineData(".mud-chip.status-chip-tertiary", "tertiary")]
    [InlineData(".mud-chip.status-chip-primary", "primary")]
    public void StatusChips_ReadTheToneTokens(string selector, string tone)
    {
        string[] rule = Declarations(selector);

        Assert.Contains($"background-color: var(--hu-tone-{tone}-soft);", rule);
        string ink = string.Equals(tone, "neutral", StringComparison.Ordinal) ? "text-subtle" : $"tone-{tone}-ink";
        Assert.Contains($"color: var(--hu-{ink});", rule);
    }

    /// <summary>The secondary status chip is the one tone no Theme differs on, so it keeps its literal recipe.</summary>
    [Fact]
    public void SecondaryStatusChip_KeepsItsLiteralRecipe()
    {
        string[] rule = Declarations(".mud-chip.status-chip-secondary");

        Assert.Contains("background-color: color-mix(in srgb, var(--mud-palette-secondary) 14%, var(--mud-palette-surface));", rule);
        Assert.Contains("color: color-mix(in srgb, var(--mud-palette-secondary) 50%, var(--mud-palette-text-primary));", rule);
    }

    /// <summary>The segmented control's indicator outline reads <c>--hu-line-control</c>.</summary>
    [Fact]
    public void SegmentedIndicator_ReadsLineControl()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "HuddleSegmentedControl.razor.css")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int at = css.IndexOf("\n.huddle-segmented-indicator {\n", StringComparison.Ordinal);
        Assert.True(at >= 0, "No .huddle-segmented-indicator rule.");
        string body = css[at..css.IndexOf('}', at)];

        Assert.Contains("border: 1px solid var(--hu-line-control);", body, StringComparison.Ordinal); // contains-ok: one rule of the scoped stylesheet source
        Assert.DoesNotContain("lines-default", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A surface trait's outlined-button rule that paints a background skips toggle items: MudToggleGroup draws an
    /// unselected segment as an outlined button, and a raised ground there turned every unselected segment of the
    /// segmented control (List | Kanban) into a white tile on the sunk track.
    /// </summary>
    [Fact]
    public void SurfaceOutlinedButtonBackgrounds_SkipToggleItems()
    {
        string css = CssText.StripComments(CssText.AppCss());
        List<string> offenders = [];
        int checkedRules = 0;

        foreach (string rule in css.Split('}'))
        {
            int open = rule.IndexOf('{', StringComparison.Ordinal);
            if (open < 0)
            {
                continue;
            }

            string body = rule[(open + 1)..];
            if (!body.Contains("background", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string selector in rule[..open].Split(','))
            {
                string trimmed = selector.Trim();
                // A toggle item is always Color.Default, so an Error-coloured outlined rule can never reach one.
                if (trimmed.StartsWith(":root:has([data-hu-surface=", StringComparison.Ordinal) && trimmed.Contains(".mud-button-outlined", StringComparison.Ordinal)
                    && !trimmed.Contains(".mud-button-outlined-error", StringComparison.Ordinal))
                {
                    checkedRules++;
                    if (!trimmed.Contains(":not(.mud-toggle-item)", StringComparison.Ordinal))
                    {
                        offenders.Add(trimmed);
                    }
                }
            }
        }

        Assert.True(checkedRules > 0, "No surface outlined-button background rule found; the scan no longer matches app.css.");
        Assert.Empty(offenders);
    }

    /// <summary>The recipes the tokens replaced no longer appear in this range; only the sites the architecture leaves literal remain.</summary>
    [Fact]
    public void Range_HasNoReplacedRecipeLeft()
    {
        string range = Range();

        Assert.DoesNotContain("--mud-palette-lines-default", range, StringComparison.Ordinal);
        Assert.DoesNotContain("color-mix(in srgb, var(--mud-palette-primary) 70%, var(--mud-palette-text-primary))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("color-mix(in srgb, var(--mud-palette-primary) 20%, var(--mud-palette-surface))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("color-mix(in srgb, var(--mud-palette-primary) 8%, var(--mud-palette-surface))", range, StringComparison.Ordinal);
        Assert.DoesNotContain("color-mix(in srgb, var(--mud-palette-primary) 50%", range, StringComparison.Ordinal);
        foreach (string hue in new[] { "text-secondary", "info", "warning", "error", "success", "tertiary" })
        {
            Assert.DoesNotContain($"color-mix(in srgb, var(--mud-palette-{hue}) 50%, var(--mud-palette-text-primary))", range, StringComparison.Ordinal);
        }

        // The two decorations the architecture leaves literal: the selected team-type icon and the strongest roster tier. The Build a team section number left
        // this list for --hu-tone-primary-tint, which gives Fluent Light's accent ink 4.5:1 (AccentInkCssTests).
        Assert.Equal(2,CssText.CountOccurrences(range, PrimaryOverSurface14));
        Assert.Equal(1, CssText.CountOccurrences(range, "color-mix(in srgb, var(--mud-palette-secondary) 14%, var(--mud-palette-surface))"));
    }

    /// <summary>The text this package owns, with LF line endings: the Teams, Sidebar, References and Task form files and the Tasks file from its first section header of this package.</summary>
    /// <returns>The range this package owns.</returns>
    private static string Range() =>
        CssText.Read(RepoPaths.App("wwwroot", "css", "teams.css"))
        + CssText.Read(RepoPaths.App("wwwroot", "css", "sidebar.css"))
        + CssText.Read(RepoPaths.App("wwwroot", "css", "references.css"))
        + CssText.Section(CssText.Read(RepoPaths.App("wwwroot", "css", "tasks.css")), RangeStart)
        + CssText.Read(RepoPaths.App("wwwroot", "css", "task-form.css"));

    /// <summary>The trimmed declarations of the first rule in this package's range whose selector text is exactly <paramref name="selector"/>.</summary>
    /// <param name="selector">The selector list as written, with <c>\n</c> between selectors.</param>
    /// <returns>One entry per line of the rule body.</returns>
    private static string[] Declarations(string selector)
    {
        string range = "\n" + Range();
        string needle = "\n" + selector + " {\n";
        int at = range.IndexOf(needle, StringComparison.Ordinal);

        // A selector that is the tail of a longer selector list (the line before ends in a comma) is not this rule.
        while (at > 0 && range[at - 1] == ',')
        {
            at = range.IndexOf(needle, at + 1, StringComparison.Ordinal);
        }

        Assert.True(at >= 0, $"No rule for '{selector}' in the range.");
        int open = range.IndexOf('{', at);
        string body = range[(open + 1)..range.IndexOf('}', open)];
        return body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

}
