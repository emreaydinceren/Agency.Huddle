using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Sessions.Fakes;

/// <summary>
/// A hand-written test double for <see cref="IRoomSessionOwner"/> (Task 22.1.t). Records every
/// <see cref="WriteAsync"/> Envelope and every report call, and holds a settable
/// <see cref="TokenBudgetSpent"/> so a test can prove a Turn is skipped without ever prompting.
/// </summary>
internal sealed class FakeRoomSessionOwner : IRoomSessionOwner
{
    private readonly Lock gate = new();
    private readonly List<ProtocolMessage> written = [];
    private readonly List<string> reportCalls = [];

    /// <inheritdoc />
    public string PersonaName { get; set; } = "Nova";

    /// <inheritdoc />
    public bool TokenBudgetSpent { get; set; }

    /// <summary>Every Envelope <see cref="WriteAsync"/> has recorded, in write order.</summary>
    public IReadOnlyList<ProtocolMessage> Written
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.written];
            }
        }
    }

    /// <summary>The name of every report method called, in call order (for example <c>ReportTurnCompleted</c>).</summary>
    public IReadOnlyList<string> ReportCalls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.reportCalls];
            }
        }
    }

    /// <summary>The running total <see cref="AddTokens"/> has accumulated.</summary>
    public long TokensAdded { get; private set; }

    /// <summary>The reason passed to the last <see cref="ReportTurnFailure"/> call, or <see langword="null"/> if never called.</summary>
    public string? LastFailureReason { get; private set; }

    /// <summary>The reason passed to the last <see cref="ReportOffline"/> call, or <see langword="null"/> if never called.</summary>
    public string? LastOfflineReason { get; private set; }

    /// <summary>The <see cref="StopReason"/> passed to the last <see cref="ReportIncompleteStop"/> call, if any.</summary>
    public StopReason? LastIncompleteStopReason { get; private set; }

    /// <summary>The exception passed to the last <see cref="ReportLoopEnded"/> call, if any.</summary>
    public Exception? LastLoopEndedException { get; private set; }

    /// <inheritdoc />
    public Task WriteAsync(ProtocolMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (this.gate)
        {
            this.written.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AddTokens(long delta) => this.TokensAdded += delta;

    /// <inheritdoc />
    public void ReportTokenBudgetSpent() => this.RecordCall(nameof(this.ReportTokenBudgetSpent));

    /// <inheritdoc />
    public void ReportTurnCompleted() => this.RecordCall(nameof(this.ReportTurnCompleted));

    /// <inheritdoc />
    public void ReportIncompleteStop(StopReason reason)
    {
        this.LastIncompleteStopReason = reason;
        this.RecordCall(nameof(this.ReportIncompleteStop));
    }

    /// <inheritdoc />
    public void ReportTurnFailure(string roomName, string reason)
    {
        this.LastFailureReason = reason;
        this.RecordCall(nameof(this.ReportTurnFailure));
    }

    /// <inheritdoc />
    public void ReportOffline(string reason)
    {
        this.LastOfflineReason = reason;
        this.RecordCall(nameof(this.ReportOffline));
    }

    /// <inheritdoc />
    public void ReportLoopEnded(string loopName, Exception? exception)
    {
        this.LastLoopEndedException = exception;
        this.RecordCall(nameof(this.ReportLoopEnded));
    }

    private void RecordCall(string name)
    {
        lock (this.gate)
        {
            this.reportCalls.Add(name);
        }
    }
}
