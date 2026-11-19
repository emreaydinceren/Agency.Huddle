namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Where a dropped Board card lands in its Team's rank order.</summary>
internal static class RankPlacement
{
    /// <summary>What a drop does to the card's rank.</summary>
    internal abstract record Change
    {
        private Change()
        {
        }

        /// <summary>Nothing is written.</summary>
        internal sealed record NoChange : Change;

        /// <summary>The rank becomes <paramref name="Rank"/>.</summary>
        /// <param name="Rank">The new rank.</param>
        internal sealed record Set(int Rank) : Change;

        /// <summary>The rank is removed.</summary>
        internal sealed record Clear : Change;
    }

    /// <summary>How far past the last ranked card, or from nothing, a new rank goes, so a later card can fit between.</summary>
    private const int Step = 10;

    /// <summary>
    /// The rank for <paramref name="dragged"/> dropped at <paramref name="index"/> among <paramref name="zone"/>. Cards of
    /// another Team are not neighbours: ranks compare only within a Team. A result equal to the card's current rank is
    /// <see cref="Change.NoChange"/>.
    /// </summary>
    /// <param name="zone">The zone's cards in display order, without the dragged card.</param>
    /// <param name="index">The drop index among <paramref name="zone"/>: the number of its cards that stay above the dragged one.</param>
    /// <param name="dragged">The dragged Task.</param>
    internal static Change Place(IReadOnlyList<TaskItem> zone, int index, TaskItem dragged)
    {
        int at = Math.Clamp(index, 0, zone.Count);
        TaskItem? above = null;
        for (int i = at - 1; i >= 0 && above is null; i--)
        {
            above = SameTeam(zone[i], dragged) ? zone[i] : null;
        }

        TaskItem? below = null;
        for (int i = at; i < zone.Count && below is null; i++)
        {
            below = SameTeam(zone[i], dragged) ? zone[i] : null;
        }

        Change change = Decide(above, below, dragged);
        return change is Change.Set set && set.Rank == dragged.Rank ? new Change.NoChange() : change;
    }

    /// <summary>The change for the two neighbours (either may be absent) before the current-rank check.</summary>
    /// <param name="above">The nearest card above of the dragged card's Team, or <see langword="null"/>.</param>
    /// <param name="below">The nearest card below of the dragged card's Team, or <see langword="null"/>.</param>
    /// <param name="dragged">The dragged Task.</param>
    private static Change Decide(TaskItem? above, TaskItem? below, TaskItem dragged)
    {
        if (above?.Rank is { } upper)
        {
            return below?.Rank is { } lower
                ? Between(above, below, upper, lower, dragged)
                : Of(upper + (long)Step);
        }

        if (above is not null)
        {
            return dragged.Rank is null ? new Change.NoChange() : new Change.Clear();
        }

        return below?.Rank is { } first ? Of(first / 2L) : Of(Step);
    }

    /// <summary>The rank between two ranked neighbours: the middle when a whole number fits, otherwise the first of their two ranks under which the default sort puts the card between them, otherwise the upper neighbour's rank.</summary>
    /// <param name="above">The card above.</param>
    /// <param name="below">The card below.</param>
    /// <param name="upper">The rank of <paramref name="above"/>.</param>
    /// <param name="lower">The rank of <paramref name="below"/>.</param>
    /// <param name="dragged">The dragged Task.</param>
    private static Change.Set Between(TaskItem above, TaskItem below, int upper, int lower, TaskItem dragged)
    {
        long gap = (long)lower - upper;
        if (gap >= 2)
        {
            return Of(upper + (gap / 2));
        }

        foreach (int candidate in (ReadOnlySpan<int>)[upper, lower])
        {
            if (SortsBetween(above, below, dragged with { Rank = candidate }))
            {
                return Of(candidate);
            }
        }

        return Of(upper);
    }

    /// <summary>Whether the default sort puts <paramref name="card"/> directly between <paramref name="above"/> and <paramref name="below"/>.</summary>
    /// <param name="above">The card above.</param>
    /// <param name="below">The card below.</param>
    /// <param name="card">The dragged card with the rank being tried.</param>
    private static bool SortsBetween(TaskItem above, TaskItem below, TaskItem card)
    {
        IReadOnlyList<TaskItem> sorted = TaskQuery.Sort([below, card, above], []);
        return ReferenceEquals(sorted[0], above) && ReferenceEquals(sorted[1], card) && ReferenceEquals(sorted[2], below);
    }

    /// <summary>A <see cref="Change.Set"/> of <paramref name="rank"/>, clamped to 1 through <see cref="int.MaxValue"/>.</summary>
    /// <param name="rank">The wanted rank, in <see langword="long"/> so the sums cannot overflow.</param>
    private static Change.Set Of(long rank) => new((int)Math.Clamp(rank, 1L, int.MaxValue));

    /// <summary>Whether <paramref name="card"/> belongs to the dragged Task's Team.</summary>
    /// <param name="card">A card in the zone.</param>
    /// <param name="dragged">The dragged Task.</param>
    private static bool SameTeam(TaskItem card, TaskItem dragged) =>
        string.Equals(card.Location.Team, dragged.Location.Team, StringComparison.OrdinalIgnoreCase);
}
