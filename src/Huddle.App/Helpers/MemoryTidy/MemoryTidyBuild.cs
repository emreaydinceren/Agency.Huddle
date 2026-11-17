namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What <see cref="MemoryTidyInputBuilder"/> returns: the input and what the pass needs to store the offset and the marker.</summary>
/// <param name="Input">The input for the call.</param>
/// <param name="Shown">How many files are shown in full.</param>
/// <param name="AllFitted">Whether every file eligible for full view was shown.</param>
/// <param name="EligibleCount">How many files are eligible for full view (topic files not marked <c>tidy: keep</c>).</param>
/// <param name="FolderFingerprint">The folder's fingerprint (§6.5) at the moment the files were read.</param>
/// <param name="Offset">The offset this input started at.</param>
internal sealed record MemoryTidyBuild(MemoryTidyInput Input, int Shown, bool AllFitted, int EligibleCount, string FolderFingerprint, int Offset);
