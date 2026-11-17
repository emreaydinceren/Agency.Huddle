namespace Agency.Huddle.Contracts;

/// <summary>What a <see cref="QueueAck"/> reports.</summary>
public enum QueueAckKind
{
    /// <summary>A Turn started; the ack carries the reply id.</summary>
    Started,

    /// <summary>A Turn finished; the ack carries the outcome.</summary>
    Finished,

    /// <summary>The client holds the Messages for later and finishes nothing; the rows stay waiting.</summary>
    Held,
}
