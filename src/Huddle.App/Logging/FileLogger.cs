using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Logging;

/// <summary>
/// An <see cref="ILogger"/> that appends one formatted entry per call to the run's log file.
/// </summary>
/// <remarks>
/// Takes its owner rather than the owner's scope provider, because <see cref="ISupportExternalScope"/>
/// lets the logging factory replace that provider after loggers have been created - a logger holding
/// the original by value would silently stop reporting scopes such as <c>RequestPath</c>.
/// </remarks>
/// <param name="categoryName">The category this logger writes under, normally a full type name.</param>
/// <param name="owner">The provider supplying the shared file, clock and current scope provider.</param>
internal sealed class FileLogger(string categoryName, FileLoggerProvider owner) : ILogger
{
    /// <summary>Pushes <paramref name="state"/> onto the ambient scope stack.</summary>
    /// <typeparam name="TState">Type of the scope state.</typeparam>
    /// <param name="state">The scope to push.</param>
    /// <returns>A handle that pops the scope when disposed.</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        owner.Scopes.Push(state);

    /// <summary>
    /// Whether <paramref name="logLevel"/> reaches the file. Every level but
    /// <see cref="LogLevel.None"/> does: the configured <c>Logging:File:LogLevel</c> filters are
    /// applied by the logging factory before it ever calls this logger.
    /// </summary>
    /// <param name="logLevel">The level to test.</param>
    /// <returns><see langword="true"/> unless <paramref name="logLevel"/> is <see cref="LogLevel.None"/>.</returns>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>Formats one entry and appends it to the run's file.</summary>
    /// <typeparam name="TState">Type of the log state.</typeparam>
    /// <param name="logLevel">Severity of the entry.</param>
    /// <param name="eventId">Event id, written only when it is non-zero.</param>
    /// <param name="state">The log state passed to <paramref name="formatter"/>.</param>
    /// <param name="exception">Exception to record, if any.</param>
    /// <param name="formatter">Produces the message text from <paramref name="state"/>.</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        StringBuilder entry = new();
        entry.Append(CultureInfo.InvariantCulture, $"{owner.Clock.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} ");
        entry.Append(Abbreviate(logLevel));
        entry.Append(' ');
        entry.Append(categoryName);

        // A zero id is the default for a plain logger.LogWarning(...) call and says nothing; the
        // hashed ids that [LoggerMessage] generates identify the message and are worth keeping.
        if (eventId.Id != 0)
        {
            entry.Append(CultureInfo.InvariantCulture, $"[{eventId.Id}]");
        }

        entry.Append(' ');
        entry.Append(message);

        owner.Scopes.ForEachScope(AppendScope, entry);

        if (exception is not null)
        {
            AppendIndented(entry, exception.ToString());
        }

        owner.Writer.Write(entry.ToString());
    }

    /// <summary>
    /// Appends one ambient scope as its own indented continuation line. A static method group rather
    /// than a lambda: <see cref="IExternalScopeProvider.ForEachScope{TState}"/> is called from an
    /// instance method of a primary-constructor type, where a <c>static</c> lambda may not reference
    /// the enclosing instance at all.
    /// </summary>
    /// <param name="scope">The scope object, as pushed by <see cref="BeginScope{TState}"/>.</param>
    /// <param name="entry">The entry being built.</param>
    private static void AppendScope(object? scope, StringBuilder entry)
    {
        if (scope is null)
        {
            return;
        }

        entry.Append(Environment.NewLine)
            .Append("    scope: ")
            .Append(Convert.ToString(scope, CultureInfo.InvariantCulture));
    }

    /// <summary>The fixed-width three-letter form of a level, so entries stay column-aligned.</summary>
    /// <param name="logLevel">The level to abbreviate.</param>
    /// <returns>A three-character abbreviation.</returns>
    private static string Abbreviate(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    /// <summary>
    /// Appends <paramref name="text"/> as continuation lines, each indented four spaces, so a stack
    /// trace stays visibly subordinate to the entry it belongs to.
    /// </summary>
    /// <param name="entry">The entry being built.</param>
    /// <param name="text">Possibly multi-line text to append.</param>
    private static void AppendIndented(StringBuilder entry, string text)
    {
        foreach (var line in text.Split(Environment.NewLine))
        {
            entry.Append(Environment.NewLine).Append("    ").Append(line);
        }
    }
}