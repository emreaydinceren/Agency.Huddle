using Bunit;
using Bunit.Rendering;
using MudBlazor;
using MudBlazor.Utilities;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the four chip styles (design-system.md "Chips"): <c>StatusChip</c> is tinted and never a solid fill,
/// the stylesheet declares the tinted, tag, filter and meta looks, and the tinted text reaches the WCAG 4.5:1
/// floor on its own fill in Huddle Light and Huddle Dark.
/// </summary>
public sealed class ChipStyleTests
{
    /// <summary>A <c>StatusChip</c> is a Small, text-variant (never filled) chip carrying <c>status-chip</c>, its colour class and its icon.</summary>
    [Theory]
    [InlineData(Color.Default, "status-chip-default")]
    [InlineData(Color.Info, "status-chip-info")]
    [InlineData(Color.Warning, "status-chip-warning")]
    [InlineData(Color.Error, "status-chip-error")]
    [InlineData(Color.Success, "status-chip-success")]
    public async Task StatusChip_IsTintedNotFilled(Color color, string colourClass)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<StatusChip>(0);
            builder.AddAttribute(1, nameof(StatusChip.Color), color);
            builder.AddAttribute(2, nameof(StatusChip.Icon), Icons.Material.Filled.Warning);
            builder.AddAttribute(3, nameof(StatusChip.ChildContent), (Microsoft.AspNetCore.Components.RenderFragment)(content => content.AddContent(0, "High")));
            builder.CloseComponent();
        });

        AngleSharp.Dom.IElement chip = cut.Find(".mud-chip");
        Assert.Contains("status-chip", chip.ClassList, StringComparer.Ordinal);
        Assert.Contains(colourClass, chip.ClassList, StringComparer.Ordinal);
        Assert.Contains("mud-chip-text", chip.ClassList, StringComparer.Ordinal);
        Assert.DoesNotContain("mud-chip-filled", chip.ClassList, StringComparer.Ordinal);
        Assert.Equal(Icons.Material.Filled.Warning, cut.FindComponent<MudChip<string>>().Instance.Icon);
    }

    /// <summary>The stylesheet declares each tinted status colour as 14% over Surface with text mixed 50% toward TextPrimary, and never as a solid fill.</summary>
    [Theory]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("success")]
    [InlineData("secondary")]
    [InlineData("tertiary")]
    public void AppCss_StatusChip_IsTintedNeverSolid(string colour)
    {
        string rule = RuleBody($".mud-chip.status-chip-{colour}");

        Assert.Contains($"background-color: color-mix(in srgb, var(--mud-palette-{colour}) 14%, var(--mud-palette-surface));", rule, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains($"color: color-mix(in srgb, var(--mud-palette-{colour}) 50%, var(--mud-palette-text-primary));", rule, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain($"background-color: var(--mud-palette-{colour});", rule, StringComparison.Ordinal);
    }

    /// <summary>The tag chip is a Wash fill with an Edge outline and TextPrimary text; the filter chip is dashed until set, then Primary-bordered with a tint; meta is plain caption text.</summary>
    [Fact]
    public void AppCss_TagFilterAndMetaChips_AreDeclared()
    {
        string tag = RuleBody(".mud-chip.tag-chip");
        Assert.Contains("background-color: var(--ink-wash);", tag, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border: 1px solid var(--ink-edge);", tag, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--mud-palette-text-primary);", tag, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

        Assert.Contains("1px dashed", RuleBody(".mud-chip.filter-chip"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        string set = RuleBody(".mud-chip.filter-chip.mud-chip-selected");
        Assert.Contains("1px solid var(--mud-palette-primary)", set, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color-mix(in srgb, var(--mud-palette-primary) 14%", set, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

        Assert.Contains("var(--mud-typography-caption-size)", RuleBody(".chip-meta"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The tinted text (status colour 50% toward TextPrimary) on its fill (14% over Surface) reaches 4.5:1 for every status colour, and for the neutral chip, in Huddle Light and Huddle Dark.</summary>
    [Fact]
    public void StatusChipText_ReachesFloor_InHuddleLightAndDark()
    {
        PaletteLight light = HuddleTheme.Light();
        PaletteDark dark = HuddleTheme.Dark();

        (string Theme, MudColor Surface, MudColor TextPrimary, MudColor[] Statuses)[] themes =
        [
            ("Huddle Light", light.Surface, light.TextPrimary, [light.Info, light.Warning, light.Error, light.Success, light.Secondary, light.TextSecondary]),
            ("Huddle Dark", dark.Surface, dark.TextPrimary, [dark.Info, dark.Warning, dark.Error, dark.Success, dark.Secondary, dark.TextSecondary]),
        ];

        foreach ((string theme, MudColor surface, MudColor textPrimary, MudColor[] statuses) in themes)
        {
            foreach (MudColor status in statuses)
            {
                double ratio = StatusChipContrast.Ratio(status, surface, textPrimary);
                Assert.True(ratio >= 4.5, $"{theme}: chip text for {status.ToString(MudColorOutputFormats.Hex)} on its tint is {ratio:0.00}:1, under 4.5:1.");
            }
        }
    }

    /// <summary>A <c>StatusChip</c> writes its own classes first and the caller's after them, so a page can name the chip without losing the look.</summary>
    [Fact]
    public async Task StatusChip_ClassOrder_IsOwnThenCallers()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<StatusChip>(0);
            builder.AddAttribute(1, nameof(StatusChip.Color), Color.Success);
            builder.AddAttribute(2, nameof(StatusChip.Class), "task-card-awake-chip");
            builder.CloseComponent();
        });

        string classes = cut.Find(".mud-chip").GetAttribute("class") ?? string.Empty;
        Assert.Contains("status-chip status-chip-success task-card-awake-chip", classes, StringComparison.Ordinal); // contains-ok: one attribute of the rendered chip, asserting token order
    }

    /// <summary>The body of the first rule whose selector list ends with <paramref name="selector"/>, read from app.css.</summary>
    private static string RuleBody(string selector)
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));
        int at = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(at >= 0, $"No rule for {selector}.");
        int open = css.IndexOf('{', at);
        return css[(open + 1)..css.IndexOf('}', open)];
    }
}
