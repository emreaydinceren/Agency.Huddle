namespace Agency.Huddle.App.Services;

/// <summary>What the Room view calls to stop an Agent in a Room (Turn Queue spec §5.9).</summary>
internal interface ITurnControl
{
    /// <summary>Closes the Agent's waiting and running rows in the Room as <c>stopped</c>, then stops its live Turn.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="ct">Cancels the stop request.</param>
    Task StopAsync(string agentId, string roomId, CancellationToken ct);
}
