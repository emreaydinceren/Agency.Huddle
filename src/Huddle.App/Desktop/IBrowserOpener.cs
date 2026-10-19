namespace Agency.Huddle.App.Desktop;

/// <summary>Opens the browser to a local loopback URL.</summary>
internal interface IBrowserOpener
{
    /// <summary>Open the browser to the given URL.</summary>
    /// <param name="url">The URL to open, validated to be http://127.0.0.1 or http://localhost with any port.</param>
    void Open(string url);
}
