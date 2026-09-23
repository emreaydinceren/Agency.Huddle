namespace Agency.Huddle.App.Acp;

/// <summary>
/// Which of RS §6.9's two truthful system-prompt texts a session gets, chosen from the resolved
/// Adapter Profile's <see cref="AdapterProfile.SessionPerRoom"/> (D28).
/// </summary>
internal enum SessionScope
{
    /// <summary>One session spans every Room the Agent is in (<c>SessionPerRoom: false</c>). Gets <c>systemPrompt.sharedSession</c>.</summary>
    Shared,

    /// <summary>One session per Room (<c>SessionPerRoom: true</c>). Gets <c>systemPrompt.roomSessions</c>, plus <c>systemPrompt.roomSessionsCarry</c> when a memory index is present.</summary>
    PerRoom,
}
