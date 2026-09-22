namespace Agency.Huddle.App.Logging;

/// <summary>
/// Settings for the per-run log file, bound from the <c>Team:FileLog</c> configuration section.
/// </summary>
/// <remarks>
/// These are read straight from <see cref="IConfiguration"/> by
/// <see cref="LoggingBuilderExtensions.AddTeamFileLogging"/> rather than through
/// <c>IOptions&lt;T&gt;</c>. The provider has to exist before <c>builder.Build()</c>, because a
/// start-up failure inside the DI container is the main thing this log is for - by the time an
/// options binder could have run, that failure has already happened and taken the process with it.
/// </remarks>
public sealed class FileLogOptions
{
    /// <summary>The configuration section these settings bind from.</summary>
    public const string SectionName = "Team:FileLog";

    /// <summary>
    /// Whether this run writes a log file. Off unless a configuration source turns it on, so that
    /// the test suite - which composes this same application once per fixture through
    /// <c>TeamWebApplicationFactory</c> - does not open a file handle per host.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Directory the log files are written to. A relative path resolves against <c>Team:DataDir</c>,
    /// so the default lands in <c>App_Data/logs</c>, which <c>.gitignore</c> already covers. An
    /// absolute path is used as given.
    /// </summary>
    public string DirectoryPath { get; init; } = "logs";

    /// <summary>
    /// How many log files to keep in <see cref="DirectoryPath"/>, this run's file included. Older
    /// files are deleted when a run starts. Zero or less keeps every file for ever.
    /// </summary>
    public int RetainedFileCount { get; init; } = 20;
}