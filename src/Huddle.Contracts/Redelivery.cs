namespace Agency.Huddle.Contracts;

/// <summary>Why a Message is sent again.</summary>
public enum Redelivery
{
    /// <summary>The Message was waiting: owed a Turn, no prompt sent.</summary>
    Waiting,

    /// <summary>A Turn for the Message was cut off.</summary>
    CutOff,
}
