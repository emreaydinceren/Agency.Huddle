namespace Agency.Huddle.App.Desktop;

/// <summary>
/// Settings for desktop host mode, bound from the <c>Team:Desktop</c> configuration section.
/// </summary>
public sealed class DesktopOptions
{
    /// <summary>The configuration section these settings bind from.</summary>
    public const string SectionName = "Team:Desktop";

    /// <summary>
    /// Whether desktop host mode is enabled. Off unless a configuration source turns it on, so
    /// that tests and development runs are unaffected. When enabled, the app listens on
    /// <c>127.0.0.1:0</c>, maintains a single-instance lock, and provides a browser-opener
    /// service and a stop endpoint.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether to open the browser automatically when the app starts. Defaults to <see langword="true"/>.
    /// </summary>
    public bool OpenBrowser { get; init; } = true;
}
