namespace Agency.Huddle.App.Components;

/// <summary>
/// What an <c>ActionButton</c> is for, which fixes its whole look (design-system.md House defaults). Public because it is a
/// component parameter's type; in <c>Agency.Huddle.App.Components</c>, like <see cref="AgentProfileCardMode"/>, because a public
/// type in a namespace ending <c>Shared</c> fails CA1716.
/// </summary>
public enum ButtonRole
{
    /// <summary>The one primary action of a view or dialog: filled in the accent.</summary>
    Primary,

    /// <summary>A secondary action beside the primary, or a standalone action in a list or card: outlined.</summary>
    Secondary,

    /// <summary>A quiet action, Cancel, Close or Back, or a link-like action in a row: text only.</summary>
    Quiet,

    /// <summary>The confirm of a destructive action, the button that destroys: filled in the Error colour.</summary>
    Danger,

    /// <summary>A trigger for a destructive action that asks first (an <c>InlineConfirm</c> or a confirm dialog): text in Error ink.</summary>
    DangerQuiet,
}
