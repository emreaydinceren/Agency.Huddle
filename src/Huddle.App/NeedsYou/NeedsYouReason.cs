namespace Agency.Huddle.App.NeedsYou;

/// <summary>Why a Room is waiting on the Human. A Room can have several at once.</summary>
[Flags]
internal enum NeedsYouReason
{
    /// <summary>No reason: never returned by the tracker.</summary>
    None = 0,

    /// <summary>An Agent Mentioned the Human in a Message the Human has not answered.</summary>
    Mention = 1,

    /// <summary>A Question card from <c>ask_human</c> is waiting.</summary>
    Question = 2,

    /// <summary>At least one Form is waiting.</summary>
    Form = 4,

    /// <summary>The Room's Budget is spent, so it is paused until the Human speaks.</summary>
    Budget = 8,
}
