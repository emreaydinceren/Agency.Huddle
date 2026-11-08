using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Copy, dates and empty states (#476): the EmptyState sentence balances its lines, so a short sentence leaves no
/// orphaned last word, and the Right sidebar's empty state is the standard one, not a quiet left-aligned override.
/// A browser is not rendered here, so the stylesheet is pinned as source text.
/// </summary>
public sealed class EmptyStateCssTests
{
    /// <summary>The sentence balances its lines, inline and full-pane alike (the Library's "No note open" left "one." alone).</summary>
    [Fact]
    public void AppCss_EmptyStateText_BalancesItsLines()
    {
        Assert.Contains("text-wrap: balance;", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".empty-state-text"), StringComparison.Ordinal);
    }

    /// <summary>The Right sidebar has no override that turns its empty state into a bare sentence.</summary>
    [Fact]
    public void AppCss_RightSidebarEmptyState_IsNotOverridden()
    {
        Assert.DoesNotContain(".right-sidebar .mud-drawer-content > .empty-state", CssText.AppCss(CssNewlines.AsIs), StringComparison.Ordinal);
    }
}
