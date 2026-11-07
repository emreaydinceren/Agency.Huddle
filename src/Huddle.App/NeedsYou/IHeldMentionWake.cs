namespace Agency.Huddle.App.NeedsYou;

/// <summary>What the Wake button on a "request held in a paused Room" Notification needs (#428).</summary>
internal interface IHeldMentionWake
{
    /// <summary>
    /// Whether the Room's Budget is still spent in memory, so Continue re-delivers the held Message and Wake is not offered.
    /// It is <see langword="false"/> after a restart, when only the Notification is left.
    /// </summary>
    /// <param name="roomId">The Room.</param>
    /// <returns><see langword="true"/> while the Room is paused.</returns>
    bool IsHeld(string roomId);

    /// <summary>
    /// Posts one Message from the Human that mentions the Agent, which starts exactly one Turn, and closes the Notification.
    /// It does nothing when the Notification is closed, the Room is still paused or wake-ups are off; it closes the Notification
    /// without posting when the Room or the Agent is gone.
    /// </summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="agentId">The Agent the Notification is about.</param>
    /// <param name="ct">Cancels the wake.</param>
    /// <returns>The Name of the Agent that was woken, or <see langword="null"/> when nobody was.</returns>
    Task<string?> WakeAsync(string roomId, string agentId, CancellationToken ct);
}
