namespace Agency.Huddle.App.Teams;

/// <summary>The outputs index of one session: the listed documents and what the cap left out (Spec §6.3, §8.1 slot 13).</summary>
/// <param name="Entries">The listed documents: Team by Team in the order given, ordinal by path within each Team.</param>
/// <param name="NotListed">How many documents the cap left out, in all folders.</param>
/// <param name="Overflows">One line per <c>outputs/</c> folder that lost documents to the cap, in listing order.</param>
internal sealed record OutputsSnapshot(IReadOnlyList<OutputsEntry> Entries, int NotListed, IReadOnlyList<OutputsOverflow> Overflows)
{
    /// <summary>A snapshot that lists <paramref name="entries"/> and has no overflow line.</summary>
    /// <param name="entries">The listed documents.</param>
    /// <param name="notListed">How many documents the cap left out.</param>
    internal OutputsSnapshot(IReadOnlyList<OutputsEntry> entries, int notListed)
        : this(entries, notListed, [])
    {
    }
}
