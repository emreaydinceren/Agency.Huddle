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
    private readonly List<(string SessionId, decimal RunningTotal, string Currency)> spendAdded = [];
    private readonly List<string?> spendRoomIds = [];
    private readonly List<string?> spendTaskIds = [];

    /// <inheritdoc />
    /// <remarks>
    /// Defaults to <c>"nova"</c>, the exact casing every caller's <c>Persona("nova", ...)</c> uses:
    /// in production <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> always reads this from that
    /// one <c>Persona.Name</c>, so it can never disagree with itself, but this fake previously
    /// defaulted to <c>"Nova"</c> - a mismatch <see cref="Agency.Huddle.App.Acp.Sessions.RoomSessionStore"/>'s
    /// file-per-Name keying only tolerated on Windows' case-insensitive filesystem (it fails on Linux,
    /// where <c>Nova.json</c> and <c>nova.json</c> are different files).
    /// </remarks>
    public string PersonaName { get; set; } = "nova";

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

    /// <summary>Every <see cref="ReadTranscriptAsync"/> call's arguments, in call order.</summary>
    public List<(string RoomId, string? AfterMessageId, string BeforeMessageId, int Max)> TranscriptReadCalls { get; } = [];

    /// <summary>
    /// Scripts <see cref="ReadTranscriptAsync"/>'s answer. Defaults to an empty, non-<see langword="null"/>
    /// <see cref="TranscriptTail"/> (D24 correction 22's rule for a scripted server that never
    /// deliberately withholds one), so a test that never touches Transcript behaviour is not made to
    /// wait out a timeout it never asked for. A test proving E-3 (refused or timed out) sets this to
    /// return <see langword="null"/>.
    /// </summary>
    public Func<string, string?, string, int, CancellationToken, Task<TranscriptTail?>> ReadTranscriptHandler { get; set; } =
        (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail(RequestId: "fake", roomId, [], Omitted: 0));

    /// <summary>Every <see cref="AgentModelOption"/> list <see cref="ReportModels"/> has been called with, in call order.</summary>
    public List<IReadOnlyList<AgentModelOption>> ReportedModels { get; } = [];

    /// <summary>When set, picks an exception <see cref="WriteAsync"/> throws for a given Envelope instead of recording it; <see langword="null"/> from the delegate lets the write through.</summary>
    public Func<ProtocolMessage, Exception?>? WriteFault { get; set; }

    /// <inheritdoc />
    public Task WriteAsync(ProtocolMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (this.WriteFault?.Invoke(message) is { } fault)
        {
            throw fault;
        }

        lock (this.gate)
        {
            this.written.Add(message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AddTokens(long delta) => this.TokensAdded += delta;

    /// <summary>Every <c>AddSpend</c> call's arguments, in call order.</summary>
    public IReadOnlyList<(string SessionId, decimal RunningTotal, string Currency)> SpendAdded
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.spendAdded];
            }
        }
    }

    /// <summary>The <c>roomId</c> every <c>AddSpend</c> call carried, in call order; <see langword="null"/> for the shared session.</summary>
    public IReadOnlyList<string?> SpendRoomIds
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.spendRoomIds];
            }
        }
    }

    /// <summary>The <c>taskId</c> every <see cref="AddSpend(string, decimal, string, string?, string?)"/> call carried, in call order; <see langword="null"/> for a warm session.</summary>
    public IReadOnlyList<string?> SpendTaskIds
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.spendTaskIds];
            }
        }
    }

    /// <inheritdoc />
    public void AddSpend(string sessionId, decimal runningTotal, string currency, string? roomId, string? taskId = null)
    {
        lock (this.gate)
        {
            this.spendAdded.Add((sessionId, runningTotal, currency));
            this.spendRoomIds.Add(roomId);
            this.spendTaskIds.Add(taskId);
        }
    }

    /// <summary>Every <see cref="SetCommands"/> call's arguments, in call order.</summary>
    public List<(string SessionId, IReadOnlyList<AvailableCommandInfo> Advertised)> CommandsSet { get; } = [];

    /// <inheritdoc />
    public void SetCommands(string sessionId, IReadOnlyList<AvailableCommandInfo> advertised)
    {
        lock (this.gate)
        {
            this.CommandsSet.Add((sessionId, advertised));
        }
    }

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

    /// <inheritdoc />
    public Task<TranscriptTail?> ReadTranscriptAsync(string roomId, string? afterMessageId, string beforeMessageId, int max, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.TranscriptReadCalls.Add((roomId, afterMessageId, beforeMessageId, max));
        }

        return this.ReadTranscriptHandler(roomId, afterMessageId, beforeMessageId, max, cancellationToken);
    }

    /// <inheritdoc />
    public void ReportModels(IReadOnlyList<AgentModelOption> models)
    {
        lock (this.gate)
        {
            this.ReportedModels.Add(models);
        }
    }

    private void RecordCall(string name)
    {
        lock (this.gate)
        {
            this.reportCalls.Add(name);
        }
    }
}
