namespace Agency.Huddle.App.Acp;

/// <summary>
/// Combines pipe liveness and Persona health into the single <see cref="PersonaStatus"/> the Teammate
/// tile, the Room header, a Room banner and a tooltip all render — four surfaces that must never
/// disagree about what one badge means. <c>AgentGateway.IsOnline(agentId)</c> answers only "a pipe
/// connection exists"; a <see cref="PersonaRunner"/> whose read loop or consumer loop has died leaves
/// that pipe open, so <c>IsOnline</c> keeps reporting <see langword="true"/> for an Agent that is
/// deaf. This is the same shape as <see cref="ReplyGate"/>: one pure function stands in for two
/// booleans at every call site, so the ordering below cannot be silently re-derived differently by
/// each of the four callers.
/// </summary>
internal static class PersonaStatusResolver
{
    /// <summary>
    /// The <see cref="PersonaStatus.Since"/> a resolved status carries when <c>health</c> is
    /// <see langword="null"/> — no <see cref="PersonaHealth"/> report has ever been made for this
    /// Persona. This function takes no clock, so on a null health record it cannot manufacture a
    /// "since when" out of nothing; returning the current instant would make an otherwise pure
    /// function's answer depend on when it happened to be called, and would move every time this
    /// function runs. <see cref="DateTimeOffset.MinValue"/> is a sentinel, not a real timestamp: a
    /// caller that renders <c>Since</c> to the Human must read this value as "unknown", never as a
    /// literal moment.
    /// </summary>
    private static readonly DateTimeOffset NoHealthRecordSince = DateTimeOffset.MinValue;

    /// <summary>
    /// <para>
    /// Resolves what one Persona's Agent should be shown as, from the two facts that can disagree:
    /// whether its pipe connection exists (<paramref name="connected"/>) and what its last health
    /// report said (<paramref name="health"/>).
    /// </para>
    /// <para>
    /// Precedence, and this ordering is the rule rather than an implementation detail:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <paramref name="health"/> is <see cref="PersonaState.Offline"/> → <b>Offline</b>, with its
    /// reason — <em>even when <paramref name="connected"/> is <see langword="true"/></em>. That
    /// combination is precisely a runner whose loops have died: the pipe is still open, but nothing
    /// is reading or writing it any more.
    /// </description></item>
    /// <item><description>
    /// otherwise, <paramref name="connected"/> is <see langword="false"/> → <b>Offline</b>, carrying
    /// <paramref name="health"/>'s reason if it has one, otherwise none.
    /// </description></item>
    /// <item><description><paramref name="health"/> is <see cref="PersonaState.Starting"/> → <b>Starting</b>.</description></item>
    /// <item><description>
    /// <paramref name="health"/> is <see cref="PersonaState.Degraded"/> → <b>Degraded</b>, with its reason.
    /// </description></item>
    /// <item><description>otherwise → <b>Online</b>.</description></item>
    /// </list>
    /// <para>
    /// Rules 1 and 2 are not interchangeable, and reversing them is the mistake this function exists
    /// to make impossible: checking <paramref name="connected"/> first would report a runner whose
    /// loops have died as Online for as long as its dead pipe happens to stay open — silently, because
    /// the combination still compiles and still passes every test written before a health signal
    /// existed. Expressed as two separate <c>if</c> statements at each of the four call sites, that
    /// reordering would be an unwritten assumption at each one rather than a single rule enforced
    /// once, here.
    /// </para>
    /// </summary>
    /// <param name="connected">Whether <c>AgentGateway.IsOnline</c> reports a pipe connection for this Agent right now.</param>
    /// <param name="health">
    /// The Persona's latest <see cref="PersonaStatus"/> from <see cref="PersonaHealth.Get"/>, or
    /// <see langword="null"/> if nothing has ever been reported for it.
    /// </param>
    /// <returns>The one <see cref="PersonaStatus"/> every UI surface should render.</returns>
    internal static PersonaStatus Resolve(bool connected, PersonaStatus? health)
    {
        if (health is { State: PersonaState.Offline })
        {
            return health;
        }

        if (!connected)
        {
            return new PersonaStatus(PersonaState.Offline, health?.Reason, health?.Since ?? PersonaStatusResolver.NoHealthRecordSince);
        }

        if (health is not null)
        {
            return health;
        }

        return new PersonaStatus(PersonaState.Online, null, PersonaStatusResolver.NoHealthRecordSince);
    }
}
