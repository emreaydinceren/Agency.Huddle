using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>What an Agent connection tells the Turn Queue: its subscription, its acknowledgements and its replies (Turn Queue spec §5.5).</summary>
internal interface ITurnAcks
{
    /// <summary>Applies one acknowledgement from a connection; the caller has checked the Agent is a Member of the Room and the ids are valid.</summary>
    /// <param name="connectionId">The connection the ack arrived on.</param>
    /// <param name="agentId">The Agent that sent it.</param>
    /// <param name="ack">The acknowledgement.</param>
    void Apply(string connectionId, string agentId, QueueAck ack);

    /// <summary>Closes the Agent's rows for these Messages as replied, whatever state they were in.</summary>
    /// <param name="agentId">The replying Agent.</param>
    /// <param name="roomId">The Room the reply was posted to.</param>
    /// <param name="inReplyTo">The Messages the reply names.</param>
    void MarkReplied(string agentId, string roomId, IReadOnlyList<string> inReplyTo);

    /// <summary>Closes the Agent's open rows for these Messages as failed, for a reply the server refused.</summary>
    /// <param name="agentId">The replying Agent.</param>
    /// <param name="roomId">The Room the reply was aimed at.</param>
    /// <param name="inReplyTo">The Messages the reply names.</param>
    void MarkRefused(string agentId, string roomId, IReadOnlyList<string> inReplyTo);

    /// <summary>Records whether the Agent's latest Hello asked for the durable queue.</summary>
    /// <param name="agentId">The Agent.</param>
    /// <param name="durableQueue"><see langword="true"/> when the Hello carried the flag.</param>
    void SetSubscribed(string agentId, bool durableQueue);
}
