using Bunit;
using Bunit.Rendering;
using Agency.Huddle.App.Search;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Search;

/// <summary>
/// Pins the round-3 global search audit rows: the Search match tint (B6a), the incomplete-results note (B6b)
/// and the scope chips (B6c).
/// </summary>
public sealed class R3SrchTests(TeamWebApplicationFactory factory) : IClassFixture<TeamWebApplicationFactory>
{
    /// <summary>A match is the Attention tint at 24% over the surface with Ink text at weight 600, not the browser's yellow.</summary>
    [Fact]
    public void SearchMatch_IsAttentionTintWithInkText()
    {
        string rule = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".search-card mark");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("color-mix(in srgb, var(--mud-palette-warning) 24%, var(--mud-palette-surface))", rule, StringComparison.Ordinal);
        Assert.Contains("color: var(--mud-palette-text-primary);", rule, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600;", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("#ffff00", rule, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The notice words are Quiet ink and the icon is sized to the 14px body text, so the icon carries the signal.</summary>
    [Fact]
    public void SearchNotice_IsQuietInkWithAnIconSizedToTheText()
    {
        string notice = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".search-notice");
        string icon = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".search-notice .search-notice-icon.mud-icon-root");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("color: var(--mud-palette-text-secondary);", notice, StringComparison.Ordinal);
        Assert.Contains("font-size: inherit;", icon, StringComparison.Ordinal);
        Assert.Contains("width: 1em;", icon, StringComparison.Ordinal);
    }

    /// <summary>"Results may be incomplete." renders an Attention-signal warning icon beside the words, and the text itself carries no signal colour.</summary>
    [Fact]
    public async Task TruncatedNotice_HasAttentionIconAndNoSignalTextColour()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);
        await SearchBarHarness.TypeAsync(cut, "team:abc");

        (await h.NextOpenCallAsync(SearchFilter.Teams, SearchScope.Active, ct)).Complete(new SearchResult([new TeamHit("abc", null, 0, MatchTier.NamePrefix)], true));
        (await h.NextOpenCallAsync(SearchFilter.Teams, SearchScope.Archived, ct)).Complete();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".search-notice .search-notice-icon.mud-warning-text")));
        Assert.Empty(cut.FindAll(".search-notice.mud-warning-text"));
    }

    /// <summary>The scope chips wear the shared Filter chip, and the lit one is marked set.</summary>
    [Fact]
    public async Task ScopeChips_WearFilterChipAndTheLitOneIsSet()
    {
        await using SearchBarHarness h = new(factory);
        IRenderedComponent<ContainerFragment> cut = h.Render();
        await SearchBarHarness.OpenAsync(cut);

        Assert.Equal(6, cut.FindAll(".mud-chip.filter-chip").Count);
        Assert.Single(cut.FindAll(".mud-chip.filter-chip.is-set"));
    }
}
