using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Search;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Components.Shared.StatusColors"/>: the colour of a Needs You reason and of a search notice, moved out
/// of their call sites so no call site chooses a status colour (UX-V14). The values are the ones the
/// call sites used: a mention and a failed or truncated search are warnings, the rest are default.
/// </summary>
public sealed class StatusColorsTests
{
    /// <summary>A mention is the one Needs You reason drawn as a warning; it is the row colour <c>NeedsYouList</c> gave it.</summary>
    [Fact]
    public void For_NeedsYouReasonMention_IsWarning()
    {
        Assert.Equal(Color.Warning, Agency.Huddle.App.Components.Shared.StatusColors.For(NeedsYouReason.Mention));
    }

    /// <summary>A question is drawn in the default colour, as <c>NeedsYouList</c> drew it.</summary>
    [Fact]
    public void For_NeedsYouReasonQuestion_IsDefault()
    {
        Assert.Equal(Color.Default, Agency.Huddle.App.Components.Shared.StatusColors.For(NeedsYouReason.Question));
    }

    /// <summary>A form waiting is drawn in the default colour, as <c>NeedsYouList</c> drew it.</summary>
    [Fact]
    public void For_NeedsYouReasonForm_IsDefault()
    {
        Assert.Equal(Color.Default, Agency.Huddle.App.Components.Shared.StatusColors.For(NeedsYouReason.Form));
    }

    /// <summary>A paused Room is drawn in the default colour, as <c>NeedsYouList</c> drew it.</summary>
    [Fact]
    public void For_NeedsYouReasonBudget_IsDefault()
    {
        Assert.Equal(Color.Default, Agency.Huddle.App.Components.Shared.StatusColors.For(NeedsYouReason.Budget));
    }

    /// <summary>"Nothing matches" is the only search notice drawn in the default colour.</summary>
    [Fact]
    public void For_SearchNoticeNoMatch_IsDefault()
    {
        Assert.Equal(Color.Default, Agency.Huddle.App.Components.Shared.StatusColors.For(SearchNoticeKind.NoMatch));
    }

    /// <summary>A truncated search is a warning, as <c>SearchBar</c> drew it.</summary>
    [Fact]
    public void For_SearchNoticeTruncated_IsWarning()
    {
        Assert.Equal(Color.Warning, Agency.Huddle.App.Components.Shared.StatusColors.For(SearchNoticeKind.Truncated));
    }

    /// <summary>A failed search is a warning, as <c>SearchBar</c> drew it.</summary>
    [Fact]
    public void For_SearchNoticeFailed_IsWarning()
    {
        Assert.Equal(Color.Warning, Agency.Huddle.App.Components.Shared.StatusColors.For(SearchNoticeKind.Failed));
    }
}
