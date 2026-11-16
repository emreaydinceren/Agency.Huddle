namespace Agency.Huddle.App.Acp;

/// <summary>What the edge found out about running a browser for Agents on this computer.</summary>
/// <param name="Enabled">Whether <c>Team:Browser:Enabled</c> allows a browser at all.</param>
/// <param name="CliPath">The Playwright MCP entry point, or <see langword="null"/> when it is not installed.</param>
/// <param name="Channel">The browser channel (<c>msedge</c> or <c>chrome</c>), or <see langword="null"/> when no browser was found.</param>
/// <param name="Headless">Whether to pass <c>--headless</c>.</param>
/// <param name="NoDisplay">Whether the computer has no display (Linux with neither <c>DISPLAY</c> nor <c>WAYLAND_DISPLAY</c> set).</param>
/// <param name="IdleTimeoutMinutes">Minutes of inactivity after which the browser closes.</param>
public sealed record BrowserLaunchFacts(
    bool Enabled,
    string? CliPath,
    string? Channel,
    bool Headless,
    bool NoDisplay,
    int IdleTimeoutMinutes)
{
    /// <summary>Whether a browser can be offered: it is enabled and both the CLI and a channel are known.</summary>
    public bool Available => this.Enabled && this.CliPath is not null && this.Channel is not null;

    /// <summary>
    /// A short sentence fragment for "No browser is available: {reason}.", or <see langword="null"/>
    /// when <see cref="Available"/>.
    /// </summary>
    public string? UnavailableReason
    {
        get
        {
            if (!this.Enabled)
            {
                return "the browser is turned off for this install";
            }

            if (this.CliPath is null)
            {
                return "Playwright MCP is not installed";
            }

            return this.Channel is null ? "no Edge or Chrome was found" : null;
        }
    }
}
