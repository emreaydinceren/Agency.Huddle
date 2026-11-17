namespace Agency.Huddle.App.Data;

/// <summary>One memory folder's row in <c>memory_tidy_state</c> (Memory Tidy spec §6.5).</summary>
/// <param name="FolderKey">The folder key, <c>agent:Name</c> or <c>huddle</c>.</param>
/// <param name="Fingerprint">The folder's fingerprint after its last Verdict; <see langword="null"/> when none was stored.</param>
/// <param name="TidiedUtc">When the folder was last tidied; <see langword="null"/> when never.</param>
/// <param name="Outcome">What the last pass did with the folder, as the pass wrote it.</param>
/// <param name="Failures">How many calls on this folder ran and failed since its last Verdict.</param>
/// <param name="FailedFingerprint">The folder's fingerprint when the last counted failure happened.</param>
/// <param name="Offset">Where the next call's files in full start, so a folder larger than one call is shown whole across passes.</param>
internal sealed record MemoryTidyState(
    string FolderKey,
    string? Fingerprint = null,
    DateTimeOffset? TidiedUtc = null,
    string? Outcome = null,
    int Failures = 0,
    string? FailedFingerprint = null,
    int Offset = 0);
