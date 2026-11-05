namespace Agency.Huddle.App.Tasks;

/// <summary>What <see cref="TaskFeedStartup"/> needs from the wake-up service to keep a "wake-ups paused" Notification across a restart.</summary>
internal interface IWakePauses
{
    /// <summary>Whether an Agent with this Name exists.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <returns><see langword="true"/> when the Agent is known.</returns>
    bool AgentExists(string name);

    /// <summary>
    /// Remembers that the Task's pause Notification is open, so a later Human change or an Allow more closes it.
    /// It delivers nothing and wakes no Agent: the hand-off that was waiting is gone with the restart.
    /// </summary>
    /// <param name="id">The Task.</param>
    void RestorePaused(TaskId id);

    /// <summary>Whether a hand-off for this Task is still held in memory, so Allow more or Continue delivers it.</summary>
    /// <param name="id">The Task.</param>
    /// <returns><see langword="true"/> when a hand-off is held; <see langword="false"/> after a restart, when only the Notification is left.</returns>
    bool IsHeld(TaskId id);

    /// <summary>
    /// Wakes the Task's current assignee once, as the Human, with a general message that a hand-off was lost, and closes the pause Notification.
    /// It does nothing when no pause is open, a hand-off is still held, wake-ups are off or the Agent is unknown.
    /// </summary>
    /// <param name="id">The Task.</param>
    /// <param name="waiting">The Agent the Notification named, used when the Task has no assignee.</param>
    /// <param name="ct">Cancels the wake.</param>
    /// <returns>The Name of the Agent that was woken, or <see langword="null"/> when nobody was.</returns>
    Task<string?> WakeAsync(TaskId id, string waiting, CancellationToken ct);
}
