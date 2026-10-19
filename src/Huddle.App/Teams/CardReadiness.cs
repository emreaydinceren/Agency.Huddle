namespace Agency.Huddle.App.Teams;

/// <summary>Whether a hire card can be hired as it stands.</summary>
internal enum CardReadiness
{
    /// <summary>Every default resolved; the card can be hired.</summary>
    Ready,

    /// <summary>A default is missing; the user must complete setup first.</summary>
    NeedsSetup,
}
