namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// The Memory index built at session start for <c>SystemPromptComposer.Compose</c>'s memory
/// overload, per FC §6.15.
/// </summary>
/// <param name="MemoryPath">The Agent's Memory folder's full path, <c>{{memoryPath}}</c> in <c>systemPrompt.memory</c>.</param>
/// <param name="Entries">The listed entries, already capped at <c>MaxMemoryEntries</c>.</param>
/// <param name="NotListed">How many more Memory files exist beyond what <see cref="Entries"/> lists.</param>
internal sealed record MemorySnapshot(string MemoryPath, IReadOnlyList<MemoryEntry> Entries, int NotListed);
