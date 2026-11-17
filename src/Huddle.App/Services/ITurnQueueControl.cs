namespace Agency.Huddle.App.Services;

/// <summary>What the rest of the app asks of the Turn Queue as a lifecycle event (Turn Queue spec §5.6).</summary>
internal interface ITurnQueueControl
{
    /// <summary>Closes the running rows of a connection that ended as stopped, unless the host itself is stopping.</summary>
    /// <param name="connectionId">The connection that ended.</param>
    void CloseRunning(string connectionId);

    /// <summary>
    /// Closes as <c>expired</c> every <c>waiting</c> row of an Agent whose Message is older than <c>ResumeWithin</c>, then returns
    /// the rest, oldest first (spec §5.7, the one age rule).
    /// </summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="now">The current time, from the injected clock.</param>
    /// <returns>The <c>waiting</c> rows left; empty when the queue is switched off.</returns>
    IReadOnlyList<WaitingTurn> TakeWaiting(string agentId, DateTimeOffset now);
}
