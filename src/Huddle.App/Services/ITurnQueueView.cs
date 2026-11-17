namespace Agency.Huddle.App.Services;

/// <summary>What the Room view reads of the Turn Queue: who has Messages waiting. It never closes or expires a row.</summary>
internal interface ITurnQueueView
{
    /// <summary>The Agents with <c>waiting</c> rows in a Room, in order of their oldest waiting Message.</summary>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>One entry per Agent with at least one waiting row; empty when the queue is switched off.</returns>
    Task<IReadOnlyList<WaitingCount>> WaitingInAsync(string roomId, CancellationToken ct);
}
