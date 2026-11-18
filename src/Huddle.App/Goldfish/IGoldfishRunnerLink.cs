using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Goldfish;

/// <summary>The supervisor's one way to reach an Agent's runner: it tells it a possession has ended.</summary>
internal interface IGoldfishRunnerLink
{
    /// <summary>Sends <see cref="GoldfishEnd"/> to the Agent's runner; a silent no-op when the Agent is offline.</summary>
    /// <param name="agentId">The possessed Agent.</param>
    /// <param name="possessionId">The possession that ended.</param>
    /// <param name="exit">How it ended.</param>
    /// <param name="ct">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent or there was nobody to send it to.</returns>
    Task SendEndAsync(string agentId, string possessionId, GoldfishExit exit, CancellationToken ct);

    /// <summary>Sends <see cref="GoldfishBegin"/> to the Agent's runner; a silent no-op when the Agent is offline.</summary>
    /// <param name="agentId">The Agent to possess.</param>
    /// <param name="begin">The message to send.</param>
    /// <param name="ct">Cancels the send.</param>
    /// <returns>A task that completes when the message is sent or there was nobody to send it to.</returns>
    Task SendBeginAsync(string agentId, GoldfishBegin begin, CancellationToken ct);
}
