using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>Where a posted Message's owed Turns are written down before the Message is appended (Turn Queue spec §5.4).</summary>
internal interface ITurnLedger
{
    /// <summary>Writes one waiting row per owed Agent that has the Turn Queue switched on.</summary>
    /// <param name="message">The Message about to be appended.</param>
    /// <param name="roomId">The Room it is posted to.</param>
    /// <param name="owedAgentIds">The Agents that owe a Turn for it.</param>
    void Record(ChatMessage message, string roomId, IReadOnlyList<string> owedAgentIds);

    /// <summary>Removes every row of a Message whose append failed.</summary>
    /// <param name="messageId">The Message.</param>
    void Forget(string messageId);
}
