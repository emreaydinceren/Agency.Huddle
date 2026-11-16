using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// A test double for <see cref="IHelperProcessRunner"/>: it returns scripted Turns, counts calls, and can hold a call
/// open until the test releases it or the call's token is cancelled. No test may reach the real runner.
/// </summary>
internal sealed class FakeHelperProcessRunner : IHelperProcessRunner
{
    private int calls;
    private int inFlight;
    private int peakInFlight;

    /// <summary>The requests received, in order.</summary>
    public List<HelperSessionRequest> Requests { get; } = [];

    /// <summary>How many calls were received.</summary>
    public int Calls => Volatile.Read(ref this.calls);

    /// <summary>The most calls that were in flight at once.</summary>
    public int PeakInFlight => Volatile.Read(ref this.peakInFlight);

    /// <summary>Answers each call; reassign per test. The default answers a Turn holding <see cref="Text"/>.</summary>
    public Func<HelperSessionRequest, CancellationToken, Task<HelperTurn>> Handler { get; set; }

    /// <summary>The text the default handler answers with.</summary>
    public string Text { get; set; } = """{"yes": true, "why": "ok"}""";

    /// <summary>When set, the default handler waits for this task (or for cancellation) before answering.</summary>
    public Task? Hold { get; set; }

    /// <summary>Creates the fake with its default handler.</summary>
    public FakeHelperProcessRunner() => this.Handler = this.Default;

    /// <inheritdoc/>
    public async Task<HelperTurn> RunAsync(AgentProcessOptions processOptions, HelperSessionRequest request, CancellationToken cancellationToken)
    {
        // Count the call only once its request is recorded: a test that waits for Calls then reads Requests would otherwise
        // see the count before the request (CI run 1615 saw Calls == 2 with one request).
        lock (this.Requests)
        {
            this.Requests.Add(request);
            _ = Interlocked.Increment(ref this.calls);
        }

        int now = Interlocked.Increment(ref this.inFlight);
        int peak;
        do
        {
            peak = Volatile.Read(ref this.peakInFlight);
        }
        while (now > peak && Interlocked.CompareExchange(ref this.peakInFlight, now, peak) != peak);

        try
        {
            return await this.Handler(request, cancellationToken);
        }
        finally
        {
            _ = Interlocked.Decrement(ref this.inFlight);
        }
    }

    /// <summary>The default handler.</summary>
    private async Task<HelperTurn> Default(HelperSessionRequest request, CancellationToken cancellationToken)
    {
        if (this.Hold is not null)
        {
            await this.Hold.WaitAsync(cancellationToken);
        }

        return new HelperTurn(this.Text, ModelOffered: true, ToolCallsRefused: 0, Cost: null);
    }
}
