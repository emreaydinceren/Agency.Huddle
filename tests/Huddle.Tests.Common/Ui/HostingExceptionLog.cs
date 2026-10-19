using System.Collections.Concurrent;
using System.Diagnostics;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Records the exceptions ASP.NET Core Hosting reports on its <c>Microsoft.AspNetCore.Hosting.UnhandledException</c>
/// diagnostic event. TestHost drops the original exception when the server pipeline throws after the response was
/// written (for example while the Blazor renderer disposes its components) and the client sees only a bare
/// <see cref="OperationCanceledException"/>; this log keeps the real one so a flaky test names its cause.
/// Process wide: every host in the test run writes to it, so read it only when a test has already failed.
/// </summary>
public static class HostingExceptionLog
{
    private const string UnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";

    private static readonly ConcurrentQueue<string> Entries = new();

    private static readonly IDisposable Subscription = DiagnosticListener.AllListeners.Subscribe(new ListenerObserver());

    /// <summary>Starts listening. Safe to call from every test; the subscription is made once per process.</summary>
    public static void Start() => _ = Subscription;

    /// <summary>The exceptions recorded so far, oldest first, each as its full <see cref="Exception.ToString"/>.</summary>
    /// <returns>A copy of the log.</returns>
    public static IReadOnlyList<string> Snapshot() => [.. Entries];

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener value)
        {
            if (string.Equals(value.Name, "Microsoft.AspNetCore", StringComparison.Ordinal))
            {
                _ = value.Subscribe(new EventObserver());
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }

    private sealed class EventObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (string.Equals(value.Key, UnhandledExceptionEvent, StringComparison.Ordinal)
                && value.Value?.GetType().GetProperty("exception")?.GetValue(value.Value) is Exception exception)
            {
                Entries.Enqueue(exception.ToString());
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}