namespace Agency.Huddle.App.Acp;

/// <summary>
/// What a Room's delivery of one Message amounted to, once every reachable Agent recipient's
/// <see cref="ReplyGate"/> decision is folded together.
/// </summary>
internal enum RoomReply
{
    /// <summary>At least one reachable Agent recipient's Reply Gate said Reply.</summary>
    Expected,

    /// <summary>Every reachable Agent recipient decided CatchUp: no teammate was addressed.</summary>
    ContextOnly,

    /// <summary>The Room's Budget is spent. ADR-0006's Continue prompt already owns this surface.</summary>
    Paused,

    /// <summary>Nobody was left to deliver to: the only Agent Members are the sender, or unreachable, or absent.</summary>
    NoRecipients,

    /// <summary>
    /// A Teammate was Mentioned by name but cannot receive the Message - Offline, Degraded, or
    /// otherwise not reachable. Deliberately its own outcome rather than folded into
    /// <see cref="ContextOnly"/>, which would be false (a Teammate WAS named), or into
    /// <see cref="NoRecipients"/>, which would be false too whenever some other Teammate did receive
    /// it. Every surface renders nothing for this today: explaining it is the deferred half of
    /// ADR-0012, and rendering nothing is how the Room avoids saying something wrong in the meantime.
    /// </summary>
    MentionedUnreachable,
}
