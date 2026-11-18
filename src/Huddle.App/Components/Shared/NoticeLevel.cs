namespace Agency.Huddle.App.Components;

/// <summary>
/// How serious a <c>Notice</c> is, which picks the colour of its leading bar and icon (design-system.md "Notices and snackbars").
/// Public because it is a component parameter's type; in <c>Agency.Huddle.App.Components</c>, like <see cref="ButtonRole"/>,
/// because a public type in a namespace ending <c>Shared</c> fails CA1716. Not <c>NoticeSeverity</c>, which
/// <c>Agency.Huddle.Acp.Abstractions</c> already declares for Adapter notices, and not MudBlazor's <c>Severity</c>, so no caller
/// names a Mud type.
/// </summary>
public enum NoticeLevel
{
    /// <summary>Something failed: a save, a load, an action the Human asked for.</summary>
    Error,

    /// <summary>Something is wrong or about to be lost, but the app still works.</summary>
    Warning,

    /// <summary>A fact the Human should know, such as a feature that is off or a view that is limited.</summary>
    Info,

    /// <summary>Something the Human asked for has happened.</summary>
    Success,
}
