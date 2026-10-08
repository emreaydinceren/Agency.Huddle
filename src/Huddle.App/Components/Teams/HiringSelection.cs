using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Components.Teams;

/// <summary>
/// The cards a <c>HiringPool</c> has staged, shared with the page that hosts it. The class is public
/// because a component parameter must be; the cards stay internal.
/// </summary>
public sealed class HiringSelection
{
    /// <summary>Gets the staged cards, the manager first.</summary>
    internal IReadOnlyList<CardState> Cards { get; private set; } = [];

    /// <summary>Replaces the staged cards.</summary>
    /// <param name="cards">The cards now staged.</param>
    internal void Replace(IReadOnlyList<CardState> cards)
    {
        this.Cards = cards;
    }
}
