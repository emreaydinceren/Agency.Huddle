using System.Globalization;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The segmented control fits the Feed's nine Categories: the sliding indicator has a position for every segment, and in a column
/// narrower than its labels the Feed's control scrolls sideways instead of squeezing the segments until their labels overlap.
/// </summary>
public sealed class SegmentedControlFitTests
{
    /// <summary>The most segments the indicator can follow; the Feed's Category control is the largest user.</summary>
    private const int IndicatorPositions = 9;

    private static string SegmentedCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "HuddleSegmentedControl.razor.css")).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FeedPageCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "FeedPage.razor.css")).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Each segment, up to the ninth, moves the indicator one segment width per step. Without a rule the indicator stays under the
    /// first segment while a later one is selected, as it did for the Feed's Gates, Helpers, Devices and Second Brain.
    /// </summary>
    /// <param name="position">The selected segment, from one.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Indicator_FollowsEverySegmentUpToNine(int position)
    {
        string selector = string.Create(
            CultureInfo.InvariantCulture,
            $".huddle-segmented-control:has(.mud-toggle-item:nth-child({position}).huddle-segmented-active-item) .huddle-segmented-indicator");

        string body = CssText.RuleBody(SegmentedCss(), selector);

        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"transform: translateX({(position - 1) * 100}%);"), body.Trim());
    }

    /// <summary>The Feed's Category control has no more segments than the indicator can follow.</summary>
    [Fact]
    public void FeedCategories_FitTheIndicator()
    {
        Assert.InRange(FeedCategories.Choices.Count, 2, IndicatorPositions);
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
