namespace Agency.Huddle.App.Memory;

/// <summary>One Helper step as <see cref="MemoryWriter.ReplaceAndRemoveAsync"/> applies it (Memory Tidy spec §6.11).</summary>
/// <param name="Write">The file to write, or <see langword="null"/> for a drop.</param>
/// <param name="Remove">The files to remove once the write has landed.</param>
internal sealed record MemoryTidyChange(MemoryTidyWrite? Write, IReadOnlyList<MemoryTidyRemoval> Remove);
