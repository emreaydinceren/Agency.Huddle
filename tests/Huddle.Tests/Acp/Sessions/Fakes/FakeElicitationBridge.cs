using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// A hand-written test double for <see cref="IElicitationBridge"/>. Records every request with the token
/// it was handed, and by default holds each one open until the test answers it through
/// <see cref="BridgeCall.Answer"/> or the token is cancelled, exactly as the real bridge waits for the Human.
/// </summary>
internal sealed class FakeElicitationBridge : IElicitationBridge
{
    private readonly Lock gate = new();
    private readonly List<BridgeCall> calls = [];

    /// <summary>
    /// Replaces the default "wait for the answer or the token" behaviour when set, so a test can make the
    /// bridge throw or return at once.
    /// </summary>
    public Func<BridgeCall, Task<ElicitationResult>>? Handler { get; set; }

    /// <summary>Every request received so far, in arrival order.</summary>
    public IReadOnlyList<BridgeCall> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc />
    public async Task<ElicitationResult> RequestAsync(ElicitationContext context, ElicitationRequest request, CancellationToken cancellationToken)
    {
        BridgeCall call = new(context, request, new TaskCompletionSource<ElicitationResult>(TaskCreationOptions.RunContinuationsAsynchronously), cancellationToken);
        lock (this.gate)
        {
            this.calls.Add(call);
        }

        if (this.Handler is { } handler)
        {
            return await handler(call);
        }

        return await call.Answer.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Polls until <paramref name="count"/> requests have arrived, or fails the test after five seconds.</summary>
    /// <param name="count">How many requests to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The requests received so far.</returns>
    public async Task<IReadOnlyList<BridgeCall>> WaitForCallsAsync(int count, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (this.Calls.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The bridge did not receive the expected number of requests before the test's timeout.");
            }

            await Task.Delay(10, cancellationToken);
        }

        return this.Calls;
    }

    /// <summary>One request the bridge received.</summary>
    /// <param name="Context">The Room and Agent the request was made for.</param>
    /// <param name="Request">The form the agent asked for.</param>
    /// <param name="Answer">What a test completes to answer this request.</param>
    /// <param name="Token">The token the bridge was told to stop waiting on.</param>
    public sealed record BridgeCall(
        ElicitationContext Context,
        ElicitationRequest Request,
        TaskCompletionSource<ElicitationResult> Answer,
        CancellationToken Token);
}
