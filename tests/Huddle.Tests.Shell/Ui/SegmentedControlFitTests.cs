using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The segmented control fits any number of options: the sliding indicator is placed from the options list (its count and the
/// selected index), not from one stylesheet rule per position, and in a column narrower than its labels the Feed's control scrolls
/// sideways instead of squeezing the segments until their labels overlap.
/// </summary>
public sealed class SegmentedControlFitTests
{
    /// <summary>More options than any caller has today (the Feed's nine Categories), so the indicator is not capped at nine.</summary>
    private const int ManyOptions = 12;

    private static string SegmentedCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "SegmentedControl.razor.css")).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FeedPageCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "FeedPage.razor.css")).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The options 1 to <paramref name="count"/>, labelled with their number.</summary>
    /// <param name="count">How many options.</param>
    /// <returns>The options list.</returns>
    private static List<SegmentedOption<int>> Numbered(int count) =>
        [.. Enumerable.Range(1, count).Select(static n => new SegmentedOption<int>(n, n.ToString(CultureInfo.InvariantCulture)))];

    /// <summary>Renders a control over <paramref name="count"/> numbered options with <paramref name="selected"/> chosen.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="count">How many options.</param>
    /// <param name="selected">The selected value.</param>
    /// <returns>The rendered control.</returns>
    private static IRenderedComponent<SegmentedControl<int>> Render(MudBunitContext ctx, int count, int selected) =>
        ctx.Render<SegmentedControl<int>>(parameters => parameters
            .Add(static p => p.Options, Numbered(count))
            .Add(static p => p.Value, selected));

    /// <summary>
    /// Each selected option, up to the twelfth, tells the indicator its index and the option count. The nine fixed
    /// <c>translateX</c> rules this replaced left the indicator under the first segment for any position past them.
    /// </summary>
    /// <param name="position">The selected option, from one.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(12)]
    public async Task Indicator_FollowsEveryPosition(int position)
    {
        await using MudBunitContext ctx = new();

        IElement track = Render(ctx, ManyOptions, position).Find(".segmented-control");

        string expected = string.Create(CultureInfo.InvariantCulture, $"--segmented-count: {ManyOptions}; --segmented-index: {position - 1};");
        Assert.Equal(expected, track.GetAttribute("style"));
    }

    /// <summary>Choosing an option moves the indicator at once, before the caller has handed the new value back.</summary>
    [Fact]
    public async Task Choosing_MovesTheIndicator()
    {
        await using MudBunitContext ctx = new();
        List<int> raised = [];
        IRenderedComponent<SegmentedControl<int>> cut = ctx.Render<SegmentedControl<int>>(parameters => parameters
            .Add(static p => p.Options, Numbered(3))
            .Add(static p => p.Value, 1)
            .Add(p => p.ValueChanged, (int value) => raised.Add(value)));

        await cut.InvokeAsync(() => cut.FindAll(".mud-toggle-item")[2].Click());

        Assert.Equal([3], raised);
        Assert.Equal("--segmented-count: 3; --segmented-index: 2;", cut.Find(".segmented-control").GetAttribute("style"));
        Assert.Equal("3", cut.Find(".segmented-active-item").TextContent.Trim());
    }

    /// <summary>An option's icon and badge are drawn inside its own item, the icon before the label and the badge after it.</summary>
    [Fact]
    public async Task Option_DrawsItsIconAndBadge()
    {
        await using MudBunitContext ctx = new();
        List<SegmentedOption<string>> options =
        [
            new("a", "All"),
            new("n", "Needs you", Icon: MudBlazor.Icons.Material.Outlined.ViewList, Badge: "9"),
        ];

        IRenderedComponent<SegmentedControl<string>> cut = ctx.Render<SegmentedControl<string>>(parameters => parameters
            .Add(static p => p.Options, options)
            .Add(static p => p.Value, "a"));

        IReadOnlyList<IElement> items = [.. cut.FindAll(".mud-toggle-item")];
        Assert.Equal(2, items.Count);
        Assert.Null(items[0].QuerySelector(".segmented-badge"));
        Assert.Null(items[0].QuerySelector(".segmented-icon"));
        Assert.Equal("9", items[1].QuerySelector(".segmented-badge")?.TextContent);
        Assert.NotNull(items[1].QuerySelector(".segmented-icon"));
        Assert.Equal("Needs you 9", items[1].TextContent.Trim());
    }

    /// <summary>
    /// The indicator is one option wide and sits at its index, both read from the custom properties the component writes; no
    /// rule per position is left to go stale.
    /// </summary>
    [Fact]
    public void IndicatorCss_IsDerivedFromCountAndIndex()
    {
        string css = SegmentedCss();
        string indicator = CssText.RuleBody(css, ".segmented-indicator");

        Assert.Contains("width: calc((100% - 2 * var(--segmented-inset)) / var(--segmented-count, 1));", indicator, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("transform: translateX(calc(var(--segmented-index, 0) * 100%));", indicator, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain(":nth-child(", css, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The Feed's Category control is built from its options list, so it can never disagree with the item count.</summary>
    [Fact]
    public void FeedCategories_AreSeveralOptions()
    {
        Assert.InRange(FeedCategories.Choices.Count, 2, ManyOptions);
    }

    /// <summary>
    /// The Feed's control is laid out at its own content width inside a scroller that may shrink, so every segment is as wide as the
    /// widest label. MudToggleGroup writes <c>minmax(0, 1fr)</c> columns inline, which shrink below a label once the track is capped
    /// to its column, so the control must not be capped at the column's width.
    /// </summary>
    [Fact]
    public void FeedCategoryControl_ScrollsInsteadOfSqueezing()
    {
        string css = FeedPageCss();

        string scroller = CssText.RuleBody(css, ".feed-filter-bar .feed-category-scroller");
        Assert.Contains("overflow-x: auto;", scroller, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-width: 0;", scroller, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("max-width: 100%;", scroller, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup

        string control = CssText.RuleBody(css, ".feed-filter-bar ::deep .feed-category-control");
        Assert.Contains("width: max-content;", control, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("max-width: 100%;", control, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }
}
