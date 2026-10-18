namespace Agency.Huddle.Tests.Ui.Skins;

/// <summary>
/// Phase 2, package D: the second half of <c>app.css</c> (from "The Task List at narrower widths" to the end) and
/// <c>HuddleSegmentedControl.razor.css</c> read the skin tokens where a token's registered default is the value the
/// site wrote before. A site whose value no token default equals, or that the architecture leaves as a literal recipe,
/// is not pinned here beyond the count of recipes that must remain.
/// </summary>
public sealed class AppCssTokensDTests
{
    private const string RangeStart = "/* ---- The Task List at narrower widths";

    private const string PrimaryOverSurface14 = "color-mix(in srgb, var(--mud-palette-primary) 14%, var(--mud-palette-surface))";

    /// <summary>Each hairline between regions reads <c>--hu-line-region</c>: the Task column resizer line and the Room header's closing line.</summary>
    [Theory]
    [InlineData(".task-col-resizer::after", "background: var(--hu-line-region);")]
    [InlineData(".page-header.has-chrome", "border-bottom: 1px solid var(--hu-line-region);")]
    public void RegionLines_ReadTheToken(string selector, string declaration)
    {
        Assert.Contains(declaration, Declarations(selector));
    }

    /// <summary>The Task List's group row is painted <c>--hu-ground-page</c>.</summary>
    [Fact]
    public void TaskGroupRow_ReadsGroundPage()
    {
        Assert.Contains("background-color: var(--hu-ground-page);", Declarations(".mud-data-grid.task-list-grid .mud-table-cell.mud-datagrid-group"));
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

    /// <summary>A chosen item in a set reads <c>--hu-selected-background</c>, <c>-hover</c> and <c>--hu-selected-foreground</c>.</summary>
    [Fact]
    public void SelectedItems_ReadTheSelectedTokens()
    {
        string[] chip =Declarations(".chip-cloud-chip.is-set");
        Assert.Contains("background-color: var(--hu-selected-background);", chip);
        Assert.Contains("color: var(--hu-selected-foreground);", chip);

        string[] card =Declarations(".team-type-card-selected,\n.team-type-card-selected:hover");
        Assert.Contains("background: var(--hu-selected-background);", card);
        Assert.Contains("background: var(--hu-selected-background-hover);", Declarations(".team-type-card-selected:hover"));

        string[] pick =Declarations(".composer-picker-item[aria-selected=\"true\"]");
        Assert.Contains("background: var(--hu-selected-background);", pick);
        Assert.Contains("color: var(--hu-selected-foreground);", pick);
        Assert.Contains("background: var(--hu-selected-background-hover);", Declarations(".composer-picker-item[aria-selected=\"true\"]:hover"));

        string[] filter =Declarations(".mud-chip.filter-chip.is-set,\n.mud-chip.filter-chip.filter-chip-set,\n.mud-chip.filter-chip.mud-chip-selected");
        Assert.Contains("background-color: var(--hu-selected-background);", filter);
        Assert.Contains("color: var(--hu-selected-foreground);", filter);
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

    /// <summary>Every status chip colour except secondary reads its tone token pair; secondary keeps its literal recipe.</summary>
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
        Assert.Contains($"color: var(--hu-tone-{tone}-ink);", rule);
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

        // The three decorations the architecture leaves literal: the Build a team section number, the selected team-type icon and the strongest roster tier.
        Assert.Equal(3, Count(range, PrimaryOverSurface14));
        Assert.Equal(1, Count(range, "color-mix(in srgb, var(--mud-palette-secondary) 14%, var(--mud-palette-surface))"));
    }

    /// <summary>The text of <c>app.css</c> from this package's first section header to the end, with LF line endings.</summary>
    /// <returns>The range this package owns.</returns>
    private static string Range()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = css.IndexOf(RangeStart, StringComparison.Ordinal);
        Assert.True(start >= 0, "The range header is missing from app.css.");
        return css[start..];
    }

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

    /// <summary>How many times <paramref name="needle"/> occurs in <paramref name="text"/>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="needle">The substring to count.</param>
    /// <returns>The number of non-overlapping occurrences.</returns>
    private static int Count(string text, string needle) => text.Split(needle).Length - 1;
}
