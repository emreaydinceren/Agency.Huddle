using System.Globalization;

namespace Agency.Huddle.App.Search;

/// <summary>
/// The address a hit opens, per the table in spec §9. Every value is escaped and built with an invariant
/// format. A Teammate has no address: it opens a dialog (OP-9).
/// </summary>
internal static class SearchHitLinks
{
    /// <summary>The address a hit opens with Enter or Ctrl+Enter.</summary>
    /// <param name="hit">The hit.</param>
    /// <returns>The site-relative address, or null for a Teammate, which opens a dialog instead.</returns>
    public static string? UrlFor(SearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);

        return hit switch
        {
            TeamHit team => string.Create(CultureInfo.InvariantCulture, $"/teams/{Uri.EscapeDataString(team.Name)}"),
            TeammateHit => null,
            LibraryHit library => string.Create(CultureInfo.InvariantCulture, $"/library?root={Uri.EscapeDataString(library.RootId)}&path={Uri.EscapeDataString(library.RelativePath)}"),
            TaskHit task => string.Create(CultureInfo.InvariantCulture, $"/tasks/item/{Uri.EscapeDataString(task.Id.ToString())}"),
            RoomHit room when room.MessageId is { } messageId => string.Create(CultureInfo.InvariantCulture, $"/rooms/{Uri.EscapeDataString(room.RoomId)}?message={Uri.EscapeDataString(messageId)}"),
            RoomHit room => string.Create(CultureInfo.InvariantCulture, $"/rooms/{Uri.EscapeDataString(room.RoomId)}"),
            _ => throw new ArgumentOutOfRangeException(nameof(hit), hit, "No address is defined for this kind of search hit."),
        };
    }
}
