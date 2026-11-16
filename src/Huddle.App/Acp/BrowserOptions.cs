namespace Agency.Huddle.App.Acp;

/// <summary>Configuration for the Agents' browser, bound from <c>Team:Browser</c>.</summary>
public sealed class BrowserOptions
{
    /// <summary>The install-wide kill switch. When false no Agent is offered a browser.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The browser channel to drive (<c>msedge</c> or <c>chrome</c>). Null chooses automatically; a value here overrides the choice.</summary>
    public string? Channel { get; set; }

    /// <summary>Forces the browser to run without a window on any computer. Off by default; a computer with no display is headless regardless.</summary>
    public bool Headless { get; set; }

    /// <summary>Minutes of inactivity after which the browser is closed. Defaults to 30.</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;
}
