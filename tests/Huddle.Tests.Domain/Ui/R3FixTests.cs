using System.Text.RegularExpressions;
using MudBlazor;
using MudBlazor.Utilities;
using Agency.Huddle.App.Themes;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins the fixes the round-3 running-app check asked for: status-chip contrast in every Theme, no floating label in a compact field, the Needs you row's name width and the Library tree's inset.</summary>
public sealed partial class R3FixTests
{
    private static readonly string[] StatusNames = ["Info", "Warning", "Error", "Success", "Secondary", "Tertiary", "Primary", "Neutral"];

    private static string Rule(string css, string selector)
    {
        int start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected a rule for {selector} in app.css.");
        return css[start..css.IndexOf('}', start)];
    }

    /// <summary>The chip text of every status colour reaches 4.5:1 on its tint in every built-in Theme (round 3: Light Modern's High chip measured 3.68:1, before the VS Code ports were removed).</summary>
    [Fact]
    public void StatusChipText_ReachesFloor_InEveryTheme()
    {
        List<string> unexpected = [];
        foreach (ThemeDescriptor descriptor in ThemeCatalog.BuiltIn)
        {
            Palette palette = descriptor.Mode == ThemeMode.Light ? descriptor.Theme.PaletteLight : descriptor.Theme.PaletteDark;
            MudColor[] statuses = [palette.Info, palette.Warning, palette.Error, palette.Success, palette.Secondary, palette.Tertiary, palette.Primary, palette.TextSecondary];
            for (int index = 0; index < statuses.Length; index++)
            {
                string key = $"{descriptor.Id}/{StatusNames[index]}";
                double ratio = StatusChipContrast.Ratio(statuses[index], palette.Surface, palette.TextPrimary);
                if (ratio < 4.5)
                {
                    unexpected.Add(key);
                }
            }
        }

        Assert.True(unexpected.Count == 0, $"Chip text under 4.5:1: {string.Join(", ", unexpected)}");
    }

    /// <summary>No field wearing <c>toolbar-compact</c> sets a Label: a 28px box cannot carry a floating label, and the rule hides one so it can never be drawn over the value (round 3: Teammates "Team" over "All teams").</summary>
    [Fact]
    public void ToolbarCompactFields_HaveNoLabel()
    {
        string root = CssSource.RepoPath("src", "Huddle.App", "Components");
        List<string> labelled = [];
        foreach (string file in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
        {
            foreach (Match tag in MudFieldTag().Matches(File.ReadAllText(file)))
            {
                if (tag.Value.Contains("toolbar-compact", StringComparison.Ordinal) && LabelAttribute().IsMatch(tag.Value))
                {
                    labelled.Add(Path.GetFileName(file));
                }
            }
        }

        Assert.Empty(labelled);
        Assert.Contains("display: none;", Rule(CssText.AppCss(), ".mud-input-control.toolbar-compact .mud-input-label"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>A Needs you row gives the Room's name the width the icons and the time leave: the link pads 8px, the text starts flush, the name flexes and ellipsises, and the time is trailing and does not shrink.</summary>
    [Fact]
    public void NeedsYouRow_NameTakesTheRemainingWidth()
    {
        string css = CssText.AppCss();

        Assert.Contains("padding-inline: 8px;", Rule(css, ".needs-you-row .mud-nav-link"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("margin-inline-start: 0;", Rule(css, ".needs-you-row .mud-nav-link-text"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        string name = Rule(css, ".needs-you-name");
        Assert.Contains("flex: 1 1 auto;", name, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("min-width: 0;", name, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains(".needs-you-detail {\n    flex: 0 0 auto;\n    min-width: max-content;", css, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>A row inset 4px each side is also 8px narrower than its container, so it never overflows it: the Library tree's rows, the Settings menu's links and a selected menu item (round 3: a scrollbar under each expanded folder).</summary>
    [Theory]
    [InlineData(".library-tree .mud-treeview-item-content")]
    [InlineData(".settings-nav .mud-nav-link")]
    [InlineData(".mud-list-item.mud-selected-item")]
    [InlineData(".mud-drawer .mud-navmenu .mud-nav-link")]
    public void InsetRows_AreNarrowerByTheirInset(string selector)
    {
        string rule = Rule(CssText.AppCss(), selector);

        Assert.Contains("width: calc(100% - 8px);", rule, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("margin-inline: 4px;", rule, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The head outlet is interactive like the routes, or a PageTitle rendered by an interactive page never reaches the document title after the first load (round 3: the title stayed on the first page).</summary>
    [Fact]
    public void HeadOutlet_IsInteractive_LikeTheRoutes()
    {
        string app = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "App.razor"));

        Assert.Matches(@"<HeadOutlet\s+@rendermode=""InteractiveServer""\s*/>", app);
        Assert.Matches(@"<Routes\s+@rendermode=""InteractiveServer""\s*/>", app);
    }

    [GeneratedRegex(@"<Mud(?:Select|TextField|Autocomplete|NumericField)\b[^>]*>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex MudFieldTag();

    [GeneratedRegex(@"\sLabel=", RegexOptions.CultureInvariant)]
    private static partial Regex LabelAttribute();
}
