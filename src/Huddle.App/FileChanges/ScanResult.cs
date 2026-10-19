namespace Agency.Huddle.App.FileChanges;

/// <summary>The result of one <see cref="FolderScanner.Scan(string, FileChangesOptions, bool, string)"/> call.</summary>
/// <param name="Outcome">Whether the folder was scanned, is missing, or is too large.</param>
/// <param name="Snapshot"><see cref="FolderSnapshot.Empty"/> for <see cref="ScanOutcome.Missing"/> and <see cref="ScanOutcome.TooLarge"/>.</param>
internal sealed record ScanResult(ScanOutcome Outcome, FolderSnapshot Snapshot);
