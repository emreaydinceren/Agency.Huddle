namespace Agency.Huddle.App.Acp;

/// <summary>An amount of money in one currency, as an Adapter reported it.</summary>
/// <param name="Amount">The amount, in <paramref name="Currency"/>.</param>
/// <param name="Currency">The currency code the Adapter reported, for example <c>USD</c>.</param>
internal sealed record SpendAmount(decimal Amount, string Currency);

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
/// new spend. The figure is this run only. It is not persisted, and a restart starts from zero.
/// </remarks>
/// <param name="logger">Records an ignored report and a <see cref="SpendChanged"/> subscriber that threw.</param>
internal sealed class PersonaSpend(ILogger<PersonaSpend> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PersonaSpendState> personas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised, outside the lock, after a report actually adds to a Persona's Spend, with the Persona's
    /// Name as it was reported. Never raised for a report that adds nothing, so a repeated total does
    /// not repaint a card.
    /// </summary>
    internal event Action<string>? SpendChanged;

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
    internal void Add(string personaName, string sessionId, decimal runningTotal, string currency)
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
        }

        if (raise)
        {
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
        }
    }

    /// <summary>Forgets a removed Persona's Spend, so a new Persona that reuses the Name starts from zero.</summary>
    /// <param name="personaName">The Name of the removed Persona.</param>
    internal void Forget(string personaName)
    {
        lock (this.gate)
        {
            this.personas.Remove(personaName);
        }
    }

    private void RaiseSpendChanged(string personaName)
    {
        Action<string>? handlers = this.SpendChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<string> handler in handlers.GetInvocationList().Cast<Action<string>>())
        {
            try
            {
                handler(personaName);
            }
            catch (Exception ex)
            {
                // One broken subscriber, typically a card that was disposed mid-event, must not stop the others.
                logger.LogWarning(ex, "A spend-changed subscriber threw for persona '{PersonaName}'.", personaName);
            }
        }
    }

    /// <summary>The mutable state one Persona accumulates behind <see cref="gate"/>; never handed out.</summary>
    private sealed class PersonaSpendState
    {
        /// <summary>Total spend so far, by currency.</summary>
        public Dictionary<string, decimal> Sums { get; } = new(StringComparer.Ordinal);

        /// <summary>The last running total seen, by Adapter session id and currency.</summary>
        public Dictionary<(string SessionId, string Currency), decimal> LastTotals { get; } = [];
    }
}
