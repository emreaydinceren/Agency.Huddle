using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.Acp.Tests.Fakes;

/// <summary>
/// An <see cref="IElicitationScope"/> that records every request and the token it was given, and
/// answers through a <see cref="Script"/> the test sets (default: <see cref="ElicitationCancelled"/>).
/// </summary>
internal sealed class RecordingElicitationScope : IElicitationScope
{
    /// <summary>A script that never answers by itself: it waits until the token it was given is cancelled, then throws the cancellation.</summary>
    internal static readonly Func<ElicitationRequest, CancellationToken, Task<ElicitationResult>> BlockUntilCancelled =
        static async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new ElicitationCancelled();
        };

    private readonly Lock gate = new();

    private readonly List<ElicitationRequest> requests = [];

    private readonly List<CancellationToken> tokens = [];

    private readonly TaskCompletionSource<CancellationToken> firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets or sets what <see cref="ElicitAsync"/> does after recording the request.</summary>
    internal Func<ElicitationRequest, CancellationToken, Task<ElicitationResult>> Script { get; set; } =
        RecordingElicitationScope.Answer(new ElicitationCancelled());

    /// <summary>Gets every request received, in arrival order.</summary>
    internal IReadOnlyList<ElicitationRequest> Requests
    {
        get
        {
            lock (this.gate)
            {
                return this.requests.ToArray();
            }
        }
    }

    /// <summary>Gets the token each request was given, in arrival order.</summary>
    internal IReadOnlyList<CancellationToken> Tokens
    {
        get
        {
            lock (this.gate)
            {
                return this.tokens.ToArray();
            }
        }
    }

    /// <summary>Gets a task that completes with the token the first request was given, once it arrives.</summary>
    internal Task<CancellationToken> FirstEntered => this.firstEntered.Task;

    /// <summary>Builds a script that answers every request with <paramref name="result"/>.</summary>
    /// <param name="result">The answer to give.</param>
    /// <returns>The script.</returns>
    internal static Func<ElicitationRequest, CancellationToken, Task<ElicitationResult>> Answer(ElicitationResult result)
    {
        return (_, _) => Task.FromResult(result);
    }

    /// <summary>Builds a script whose returned task is faulted with <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception to fault with.</param>
    /// <returns>The script.</returns>
    internal static Func<ElicitationRequest, CancellationToken, Task<ElicitationResult>> Fault(Exception exception)
    {
        return (_, _) => Task.FromException<ElicitationResult>(exception);
    }

    /// <summary>Builds a script that waits for <paramref name="release"/> and answers with its result.</summary>
    /// <param name="release">The gate the test completes to let the request answer.</param>
    /// <returns>The script.</returns>
    internal static Func<ElicitationRequest, CancellationToken, Task<ElicitationResult>> WaitFor(TaskCompletionSource<ElicitationResult> release)
    {
        return (_, _) => release.Task;
    }

    /// <summary>Returns a task that completes when <paramref name="token"/> is cancelled.</summary>
    /// <param name="token">The token to watch.</param>
    /// <returns>A task that completes on cancellation.</returns>
    internal static Task WhenCancelled(CancellationToken token)
    {
        TaskCompletionSource cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = token.Register(() => cancelled.TrySetResult());
        return cancelled.Task;
    }

    /// <inheritdoc />
    public Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.requests.Add(request);
            this.tokens.Add(cancellationToken);
        }

        _ = this.firstEntered.TrySetResult(cancellationToken);
        return this.Script(request, cancellationToken);
    }
}
