using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Display-only Spend per Persona since the app started: what each Persona's Adapter has reported
/// costing, summed correctly through session re-opens and Adapter restarts. It limits nothing. There
/// is deliberately no method here that answers "is this too much?", and nothing on the Turn path reads
/// it, so it cannot become a Budget by accident (the Turn detail spec, section 6.6, T13).
/// </summary>
/// <remarks>
/// An Adapter reports a <em>running total</em> per session, not an amount per Turn, so each report is
/// turned into an increase against the last total seen for the same session and currency. The memory
/// is keyed by session id rather than by <c>RoomSession</c> instance: a Room Session closed and
/// re-opened by <c>session/resume</c> in the same Adapter process presents the same id and the same
/// total, which adds nothing; a restarted Adapter presents the same id and a lower total, which is
/// new spend. <see cref="Get"/> is this run only and starts from zero after a restart; every increase is also
/// written through to a <see cref="PersonaSpendStore"/>, and <see cref="GetPersisted"/> reads the lasting total.
/// </remarks>
/// <param name="logger">Records an ignored report and a <see cref="SpendChanged"/> subscriber that threw.</param>
/// <param name="store">Receives every increase, so Spend survives a restart.</param>
/// <param name="roomStore">Receives every increase that a Room Session reported, against that Room, so a Project's Spend can be read from its Rooms.</param>
internal sealed class PersonaSpend(ILogger<PersonaSpend> logger, PersonaSpendStore store, RoomSpendStore roomStore)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PersonaSpendState> personas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised, outside the lock, after a report actually adds to a Persona's Spend, with the Persona's
    /// Name as it was reported. Never raised for a report that adds nothing, so a repeated total does
    /// not repaint a card.
    /// </summary>
    internal event Action<string>? SpendChanged;

    /// <summary>
    /// Raised, outside the lock and before <see cref="SpendChanged"/>, with the Persona's Name as it was reported, the currency and the
    /// amount that report added. A ledger that charges each increase to the scopes the Persona is in at that moment subscribes here.
    /// </summary>
    internal event Action<SpendIncrease>? SpendAdded;

    /// <summary>How many handlers are subscribed to <see cref="SpendChanged"/>. Test seam only: it proves a disposed card unsubscribed.</summary>
    internal int SubscriberCount => this.SpendChanged?.GetInvocationList().Length ?? 0;

    /// <summary>
    /// Records one running total an Adapter session reported. A negative total or a blank currency is
    /// ignored, because a bad figure must not reach a card.
    /// </summary>
    /// <param name="personaName">The Persona whose Adapter reported it, compared without regard to case.</param>
    /// <param name="sessionId">The Adapter session the total belongs to.</param>
    /// <param name="runningTotal">The session's running total so far.</param>
    /// <param name="currency">The currency of <paramref name="runningTotal"/>.</param>
    /// <param name="taskId">The Task the Spend belongs to, or <see langword="null"/> when it belongs to none.</param>
    /// <param name="roomId">
    /// The Room the reporting session belongs to, or <see langword="null"/> for the shared session. The increase is also recorded against the Room, in
    /// the same lock and before either event is raised, because the Needs You cap reads Project Spend when <see cref="SpendChanged"/> fires.
    /// </param>
    internal void Add(string personaName, string sessionId, decimal runningTotal, string currency, string? taskId = null, string? roomId = null)
    {
        if (runningTotal < 0 || string.IsNullOrWhiteSpace(currency))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Ignored a spend report for persona '{PersonaName}': the amount was negative or the currency blank.", personaName);
            }

            return;
        }

        bool raise;
        decimal added;
        lock (this.gate)
        {
            if (!this.personas.TryGetValue(personaName, out PersonaSpendState? state))
            {
                state = new PersonaSpendState();
                this.personas[personaName] = state;
            }

            (string, string) key = (sessionId, currency);
            state.LastTotals.TryGetValue(key, out decimal previous);
            state.LastTotals[key] = runningTotal;

            // A total at or above the last one is normal growth. Below it, the Adapter process is new
            // and its counter restarted, so the whole lower total is spend that has not been counted.
            decimal delta = runningTotal >= previous ? runningTotal - previous : runningTotal;
            if (delta > 0)
            {
                state.Sums.TryGetValue(currency, out decimal sum);
                state.Sums[currency] = sum + delta;
            }

            raise = delta > 0;
            added = delta;
            if (raise)
            {
                store.Add(personaName, taskId, currency, delta);
                if (!string.IsNullOrWhiteSpace(roomId))
                {
                    roomStore.Add(roomId, currency, delta);
                }
            }
        }

        if (raise)
        {
            EventRaise.Isolated(this.SpendAdded, new SpendIncrease(personaName, currency, added, roomId), ex => logger.LogWarning(ex, "A spend-added subscriber threw for persona '{PersonaName}'.", personaName));
            this.RaiseSpendChanged(personaName);
        }
    }

    /// <summary>The Spend reported for <paramref name="personaName"/>, one entry per currency, ordered by currency code.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    /// <returns>A snapshot; empty when the Persona has reported nothing, so a card shows no line rather than a zero.</returns>
    internal IReadOnlyList<SpendAmount> Get(string personaName)
    {
        lock (this.gate)
        {
            if (!this.personas.TryGetValue(personaName, out PersonaSpendState? state))
            {
                return [];
            }

            return [.. state.Sums
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SpendAmount(pair.Value, pair.Key))];
        }
    }

    /// <summary>Moves a renamed Persona's Spend, and its per-session memory, to its new Name.</summary>
    /// <param name="oldName">The Name the Persona had.</param>
    /// <param name="newName">The Name it has now.</param>
    internal void Rename(string oldName, string newName)
    {
        lock (this.gate)
        {
            if (this.personas.Remove(oldName, out PersonaSpendState? state))
            {
                this.personas[newName] = state;
            }

            store.Rename(oldName, newName);
        }
    }

    /// <summary>Forgets a removed Persona's Spend, so a new Persona that reuses the Name starts from zero.</summary>
    /// <param name="personaName">The Name of the removed Persona.</param>
    internal void Forget(string personaName)
    {
        lock (this.gate)
        {
            this.personas.Remove(personaName);
            store.Remove(personaName);
        }
    }

    /// <summary>The Spend persisted for <paramref name="personaName"/> across restarts, one entry per currency.</summary>
    /// <param name="personaName">The Persona, compared without regard to case.</param>
    /// <returns>A snapshot; empty when nothing is stored.</returns>
    internal IReadOnlyList<SpendAmount> GetPersisted(string personaName) => ToAmounts(store.ForPersona(personaName));

    private static List<SpendAmount> ToAmounts(IReadOnlyList<StoredSpend> stored) =>
        [.. stored.OrderBy(static spend => spend.Currency, StringComparer.Ordinal).Select(static spend => new SpendAmount(spend.Amount, spend.Currency))];

    private void RaiseSpendChanged(string personaName)
    {
        // One broken subscriber, typically a card that was disposed mid-event, must not stop the others.
        EventRaise.Isolated(this.SpendChanged, personaName, ex => logger.LogWarning(ex, "A spend-changed subscriber threw for persona '{PersonaName}'.", personaName));
    }

    /// <summary>One increase a report added: who spent it, in which currency, and how much.</summary>
    /// <param name="PersonaName">The Persona's Name as it was reported.</param>
    /// <param name="Currency">The currency of the report.</param>
    /// <param name="Amount">The amount this report added, positive.</param>
    /// <param name="RoomId">The Room the Turn ran in, or <see langword="null"/> when none was given; it is already recorded when this is raised.</param>
    internal readonly record struct SpendIncrease(string PersonaName, string Currency, decimal Amount, string? RoomId = null);

    /// <summary>The mutable state one Persona accumulates behind <see cref="gate"/>; never handed out.</summary>
    private sealed class PersonaSpendState
    {
        /// <summary>Total spend so far, by currency.</summary>
        public Dictionary<string, decimal> Sums { get; } = new(StringComparer.Ordinal);

        /// <summary>The last running total seen, by Adapter session id and currency.</summary>
        public Dictionary<(string SessionId, string Currency), decimal> LastTotals { get; } = [];
    }
}
