namespace Agency.Huddle.App.Goldfish;

/// <summary>Answers which possession, if any, an Agent has in a Room (correction slice 3 §1.4).</summary>
internal interface IPossessionLookup
{
    /// <summary>The open possession's id when the Agent's possession in that Room is Working, Waiting or Returning.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <returns>The possession id, or <see langword="null"/> for a Starting, ended or absent possession.</returns>
    string? PossessionOf(string agentId, string roomId);

    /// <summary>Whether <see cref="PossessionOf"/> answers a possession.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <returns><see langword="true"/> when the Agent is possessed there.</returns>
    bool IsPossessed(string agentId, string roomId) => this.PossessionOf(agentId, roomId) is not null;
}
