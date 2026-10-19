namespace Agency.Huddle.App.Services;

/// <summary>
/// What <see cref="ChatService.ExtendBudgetAsync"/> actually did. A caller that only reads the old
/// <see langword="bool"/> return cannot tell "nothing to redeliver" from "not paused" from "granted" -
/// all three rendered as the same silence in the Room view, which is issue #40. Naming the three
/// outcomes lets a later phase explain the one the Human is looking at instead of guessing.
/// </summary>
public enum ExtendResult
{
    /// <summary>The Room was paused, is now extended, and its last Message went out again.</summary>
    Granted,

    /// <summary>The Room was not paused, so there was nothing for an extension to grant.</summary>
    NotPaused,

    /// <summary>The Room holds no Message at all, so there is nothing an extension could redeliver.</summary>
    NothingToRedeliver,
}