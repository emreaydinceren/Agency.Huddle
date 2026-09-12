namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly Lock gate = new Lock();

    private readonly List<LogEntry> entries = new List<LogEntry>();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.ToArray();
            }
        }
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        string message = formatter(state, exception);
        lock (this.gate)
        {
            this.entries.Add(new LogEntry(logLevel, message, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new NullScope();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }
}

internal sealed class ListLoggerFactory : ILoggerFactory
{
    private readonly ListLogger<object> logger = new ListLogger<object>();

    internal ListLogger<object> Logger => this.logger;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return this.logger;
    }

    public void Dispose()
    {
    }
}
