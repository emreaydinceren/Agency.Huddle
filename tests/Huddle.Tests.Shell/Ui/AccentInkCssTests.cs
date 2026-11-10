using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Visual audit, area J, #468: accent and status text on a tinted fill reads from the 70% accent mix (<c>--hu-accent-ink</c>) or
/// the success ink, never the raw palette colour. A browser is not rendered here, so the stylesheets are pinned as source text; the
/// skin contrast pairs hold the ratios, and the browser check is the proof of the measured values.
/// </summary>
public sealed class AccentInkCssTests
{
    private const string AccentInkColour = @"(?<![\w-])color:\s*var\(--hu-accent-ink\)";

    /// <summary>The Task id's hash prefix in a Feed chip is accent ink: the raw primary was 4.1:1 on an unseen card in Cobalt Soft.</summary>
    [Fact]
    public void FeedSegmentsCss_TaskIdPrefix_IsAccentInk()
    {
        string css = CssText.Read(RepoPaths.App("Components", "Feed", "FeedSegments.razor.css"));

        string body = CssText.RuleBodyLoose(css, ".feed-segments ::deep .feed-chip-id");

        Assert.Matches(AccentInkColour, body);
    }

    /// <summary>A Library backlink's reference, 12px on a primary tint, is accent ink.</summary>
    [Fact]
    public void AppCss_BacklinkReference_IsAccentInk()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".backlink-item-ref");

        Assert.Matches(AccentInkColour, body);
    }

    /// <summary>
    /// The Build a team step number sits on the faint primary tint, the same fill a backlink uses, so Fluent Light's accent
    /// reaches 4.5:1 on it (the 14% mix it replaced gave 4.40:1).
    /// </summary>
    [Fact]
    public void AppCss_BuildTeamStepNumber_SitsOnThePrimaryTint()
    {
        string body = CssText.RuleBody(CssText.AppCss(), ".build-team-section-number");

        Assert.Matches(@"(?<![\w-])background:\s*var\(--hu-tone-primary-tint\)", body);
        Assert.Matches(AccentInkColour, body);
    }

    /// <summary>
    /// The Live chip is a Status chip (Success), so its look comes from the one Status block and the Feed's own rule sets only its place: the
    /// wrapper carries the page's scope attribute, which MudChip's own root does not, so a bare <c>::deep</c> rule on the chip never matched.
    /// </summary>
    [Fact]
    public void FeedPageCss_LiveChip_OnlySetsItsPlace_ThroughAScopedWrapper()
    {
        string css = CssText.Read(RepoPaths.App("Components", "Pages", "FeedPage.razor.css"));

        string body = CssText.RuleBodyLoose(css, ".feed-live-wrap ::deep .feed-live-chip");

        Assert.Matches(@"(?<![\w-])margin:\s*0 0 0 8px", body);
        Assert.DoesNotMatch(@"(?<![\w-])(color|background|border|height):", body);
    }
}
