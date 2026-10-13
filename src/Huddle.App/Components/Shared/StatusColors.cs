using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Search;
using MudBlazor;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// The status colour of a Needs You reason and of a search notice, chosen here rather than at the
/// call site (UX-V14, design-system.md "Colour").
/// </summary>
internal static class StatusColors
{
    /// <summary>A mention is a warning; the other reasons are default.</summary>
    /// <param name="reason">The single reason on a Needs You row.</param>
    /// <returns>The colour the row's icon takes.</returns>
    internal static Color For(NeedsYouReason reason) => reason switch
    {
        NeedsYouReason.Mention => Color.Warning,
        _ => Color.Default,
    };

    /// <summary>A failed or truncated search is a warning; "nothing matches" is default.</summary>
    /// <param name="kind">The notice kind.</param>
    /// <returns>The colour the notice takes.</returns>
    internal static Color For(SearchNoticeKind kind) => kind switch
    {
        SearchNoticeKind.Truncated or SearchNoticeKind.Failed => Color.Warning,
        _ => Color.Default,
    };
}
