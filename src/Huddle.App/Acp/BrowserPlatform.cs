namespace Agency.Huddle.App.Acp;

/// <summary>The operating systems whose standard browser locations <see cref="BrowserServerLocator"/> knows.</summary>
public enum BrowserPlatform
{
    /// <summary>Windows.</summary>
    Windows,

    /// <summary>macOS.</summary>
    MacOS,

    /// <summary>Linux and anything else Unix-like.</summary>
    Linux,
}
