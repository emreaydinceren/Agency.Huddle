namespace Agency.Huddle.App.Desktop;

/// <summary>A no-op browser opener that does nothing.</summary>
internal sealed class NoopBrowserOpener : IBrowserOpener
{
    /// <summary>Do nothing.</summary>
    /// <param name="url">Ignored.</param>
    public void Open(string url)
    {
    }
}
