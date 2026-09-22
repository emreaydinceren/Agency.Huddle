namespace Agency.Huddle.App.Logging;

/// <summary>
/// Registers the per-run file log on a host's <see cref="ILoggingBuilder"/>.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Adds a logging provider that writes this run to its own file, when <c>Team:FileLog:Enabled</c>
    /// is true. Does nothing otherwise, so the default composition opens no file.
    /// </summary>
    /// <remarks>
    /// Call this on <c>builder.Logging</c> <em>before</em> <c>builder.Build()</c>. Service
    /// registration is resolved inside <c>Build</c>, and a constructor that throws there - an option
    /// value that will not convert, a data directory that cannot be created - terminates the process
    /// before any provider added afterwards would exist to record it.
    /// </remarks>
    /// <param name="builder">The host's logging builder.</param>
    /// <param name="configuration">Configuration carrying <c>Team:FileLog</c> and <c>Team:DataDir</c>.</param>
    /// <returns><paramref name="builder"/>, so calls can be chained.</returns>
    public static ILoggingBuilder AddTeamFileLogging(this ILoggingBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        FileLogOptions options = configuration.GetSection(FileLogOptions.SectionName).Get<FileLogOptions>() ?? new FileLogOptions();
        if (!options.Enabled)
        {
            return builder;
        }

        // Team:DataDir is read directly rather than through TeamOptions, for the same reason the
        // whole provider is built here: IOptions<TeamOptions> is not resolvable until the container
        // exists. PostConfigure makes the same value absolute for everything downstream, so this
        // resolves it the same way to land in the same App_Data.
        var dataDir = configuration[$"{TeamOptions.SectionName}:DataDir"] ?? "App_Data";
        var directoryPath = Path.IsPathRooted(options.DirectoryPath)
            ? options.DirectoryPath
            : Path.GetFullPath(Path.Combine(dataDir, options.DirectoryPath));

        var environmentName = configuration[HostDefaults.EnvironmentKey] ?? "Unknown";

        // Registered through a factory rather than builder.AddProvider(instance), so that the file
        // handle has an owner. AddProvider stores a pre-built instance, and nothing disposes one of
        // those: the DI container does not track constant instances as disposables, and LoggerFactory
        // only disposes providers it created itself - it leaves the ones injected into it to the
        // container. A factory registration makes the container the owner, so the file is closed on
        // shutdown instead of being held until the process exits.
        builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(
            directoryPath,
            options.RetainedFileCount,
            environmentName,
            TimeProvider.System));

        return builder;
    }
}