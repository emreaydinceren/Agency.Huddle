namespace Agency.Huddle.App.Logging;

/// <summary>
/// Supplies <see cref="FileLogger"/> instances that all share one <see cref="FileLogWriter"/>, so a
/// run produces a single file however many categories log to it.
/// </summary>
/// <remarks>
/// The <c>File</c> alias is what makes <c>Logging:File:LogLevel</c> in <c>appsettings.json</c> apply
/// to this provider, exactly as <c>Logging:Console:LogLevel</c> does to the console one.
/// </remarks>
[ProviderAlias("File")]
internal sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    /// <summary>Creates this run's file and the provider that writes to it.</summary>
    /// <param name="directoryPath">Absolute directory to write log files into.</param>
    /// <param name="retainedFileCount">How many log files to keep, this run's included.</param>
    /// <param name="environmentName">Host environment name, recorded in the file header.</param>
    /// <param name="clock">Clock used to name the file and stamp each entry.</param>
    public FileLoggerProvider(string directoryPath, int retainedFileCount, string environmentName, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(clock);

        this.Clock = clock;
        this.Writer = new FileLogWriter(directoryPath, retainedFileCount, environmentName, clock.GetLocalNow());
    }

    /// <summary>The file every logger from this provider appends to.</summary>
    public FileLogWriter Writer { get; }

    /// <summary>The clock each logger stamps its entries with.</summary>
    public TimeProvider Clock { get; }

    /// <summary>
    /// The current ambient scope provider. Replaced by the logging factory through
    /// <see cref="SetScopeProvider"/>, which is why loggers read it from here on every entry rather
    /// than capturing it when they are created.
    /// </summary>
    public IExternalScopeProvider Scopes { get; private set; } = new LoggerExternalScopeProvider();

    /// <summary>Creates a logger for one category.</summary>
    /// <param name="categoryName">Category the logger writes under.</param>
    /// <returns>A logger appending to this run's file.</returns>
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    /// <summary>Accepts the scope provider the logging factory wants this provider's loggers to use.</summary>
    /// <param name="scopeProvider">The provider to use from now on.</param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);

        this.Scopes = scopeProvider;
    }

    /// <summary>Closes this run's file.</summary>
    public void Dispose() => this.Writer.Dispose();
}