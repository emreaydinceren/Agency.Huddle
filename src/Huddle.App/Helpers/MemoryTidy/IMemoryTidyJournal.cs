namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>The durable record of one pass. An entry is on disk when <see cref="Append"/> returns.</summary>
internal interface IMemoryTidyJournal
{
    /// <summary>Appends one entry and flushes it to disk.</summary>
    /// <param name="entry">The entry.</param>
    void Append(MemoryTidyEntry entry);
}
