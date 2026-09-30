using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// What one <see cref="RoomSession"/> needs from its Persona's runner (RS §6.1): the pipe write, the
/// Persona name for logging, and the Persona-wide health and token-Budget signals a Room Session's
/// Turns feed into. <see cref="Agency.Huddle.App.Acp.PersonaRunner"/> implements this explicitly;
/// tests fake it.
/// </summary>
internal interface IRoomSessionOwner
{
    /// <summary>The owning Persona's name, used only in log messages.</summary>
    string PersonaName { get; }

    /// <summary>Writes an Envelope on the owner's pipe connection — the runner's only door to the Room.</summary>
    /// <param name="message">The Envelope to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Rethrows on failure (finding, D22 correction 8): the <c>PostMessage</c> write is deliberately
    /// unguarded so an <see cref="IOException"/> fails the Turn. Delta and tool-activity writes keep
    /// catching locally, inside the caller, as today.
    /// </remarks>
    Task WriteAsync(ProtocolMessage message, CancellationToken cancellationToken);

    /// <summary>Whether the Persona-wide token Budget is already spent.</summary>
    bool TokenBudgetSpent { get; }

    /// <summary>Adds <paramref name="delta"/> tokens to the Persona-wide counter.</summary>
    /// <param name="delta">How many tokens to add; never negative.</param>
    void AddTokens(long delta);

    /// <summary>
    /// Reports the running cost an Adapter session has reported so far. Display only: it feeds the
    /// Teammate card's Spend line and nothing on the Turn path reads it, so it cannot spend a Budget.
    /// </summary>
    /// <param name="sessionId">The Adapter session that reported the total; the Spend memory is keyed by it.</param>
    /// <param name="runningTotal">The session's running total so far, not the amount of one Turn.</param>
    /// <param name="currency">The currency of <paramref name="runningTotal"/>.</param>
    void AddSpend(string sessionId, decimal runningTotal, string currency);

    /// <summary>Reports that the Persona-wide token Budget is spent (Degraded).</summary>
    void ReportTokenBudgetSpent();

    /// <summary>Reports a Turn that completed successfully: resets the failure streak, reports Online.</summary>
    void ReportTurnCompleted();

    /// <summary>Reports a Turn that ended without a reply: resets the failure streak, reports Degraded.</summary>
    /// <param name="reason">Which incomplete-stop reason ended the Turn.</param>
    void ReportIncompleteStop(StopReason reason);

    /// <summary>Reports a failed Turn: grows the failure streak, reports Degraded, naming the Room.</summary>
    /// <param name="roomName">The Room the failed Turn belonged to, quoted into the reported reason.</param>
    /// <param name="reason">What went wrong, in words fit to follow "A Turn in Room '{roomName}' failed — ".</param>
    void ReportTurnFailure(string roomName, string reason);

    /// <summary>Reports that the Adapter process disconnected (Offline).</summary>
    /// <param name="reason">Why, in words fit for the Human.</param>
    void ReportOffline(string reason);

    /// <summary>Reports that one of the runner's long-lived loops ended, unless the run is shutting down (Offline).</summary>
    /// <param name="loopName">Which loop ended, named in the reported reason.</param>
    /// <param name="exception">The exception that ended the loop, if any reached its catch clause.</param>
    void ReportLoopEnded(string loopName, Exception? exception);

    /// <summary>
    /// Asks the runner for a Room's Transcript range, for a Room Session's first Turn (RS §6.5): the
    /// runner writes a <see cref="ReadTranscript"/> on the pipe and waits up to 10 seconds for the
    /// matching <see cref="TranscriptTail"/>. Waiting holds the caller's admitted gate slot (D24
    /// correction 21).
    /// </summary>
    /// <param name="roomId">The Room to read.</param>
    /// <param name="afterMessageId">Start after this Message, or <see langword="null"/> for the latest Messages.</param>
    /// <param name="beforeMessageId">Stop before this Message: the one that started the Turn.</param>
    /// <param name="max">At most this many, the latest ones.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer, or <see langword="null"/> when refused, timed out, or the run is shutting down (RS §9 E-3).</returns>
    Task<TranscriptTail?> ReadTranscriptAsync(string roomId, string? afterMessageId, string beforeMessageId, int max, CancellationToken cancellationToken);

    /// <summary>
    /// Reports the models an opened session advertised (D23/D24 correction 17/23), so a Persona whose
    /// stored Model is not in the catalog is warned once per runner even when its first open is not
    /// this runner's own Human Room.
    /// </summary>
    /// <param name="models">The just-opened session's <see cref="IAgentSession.Models"/>.</param>
    void ReportModels(IReadOnlyList<AgentModelOption> models);
}
