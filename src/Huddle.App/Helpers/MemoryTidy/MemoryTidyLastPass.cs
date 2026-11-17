namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What the card says of the newest pass folder.</summary>
/// <param name="At">When the pass started.</param>
/// <param name="Counts">What the pass changed, counted from its journal.</param>
/// <param name="ListPath">The full path of the newest <c>changes.md</c> of any pass, or <see langword="null"/> when no pass wrote one.</param>
internal sealed record MemoryTidyLastPass(DateTimeOffset At, MemoryTidyCounts Counts, string? ListPath);
