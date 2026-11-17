namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What ties one call to its pass: where the copies go and where each step is written down.</summary>
/// <param name="PassId">The pass's folder name, such as <c>2026-11-22</c>.</param>
/// <param name="BackupFolder">The folder the copies of this Memory folder go to.</param>
/// <param name="Journal">Where each step is recorded before and after it is applied.</param>
internal sealed record MemoryTidyPassContext(string PassId, string BackupFolder, IMemoryTidyJournal Journal);
