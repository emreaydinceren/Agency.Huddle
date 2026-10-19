namespace Agency.Huddle.App.FileChanges;

/// <summary>What a scan of one folder found, per FC §6.4.</summary>
internal enum ScanOutcome
{
    /// <summary>The folder exists and was walked within <see cref="FileChangesOptions.MaxFilesPerFolder"/>.</summary>
    Scanned,

    /// <summary>The folder does not exist. Not a failure: its files are reported as added once it appears.</summary>
    Missing,

    /// <summary>The folder holds more than <see cref="FileChangesOptions.MaxFilesPerFolder"/> files and was not fully walked.</summary>
    TooLarge,
}
