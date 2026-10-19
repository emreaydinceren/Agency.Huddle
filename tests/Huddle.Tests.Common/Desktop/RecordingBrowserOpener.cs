using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>
/// An <see cref="IBrowserOpener"/> that records every URL it is asked to open instead of launching a browser.
/// When <see cref="ThrowOnOpen"/> is set it throws that exception from <see cref="Open"/> after recording the URL.
/// </summary>
internal sealed class RecordingBrowserOpener : IBrowserOpener
{
    private readonly List<string> opened = [];

    /// <summary>Gets the URLs passed to <see cref="Open"/>, in call order.</summary>
    public IReadOnlyList<string> Opened => this.opened;

    /// <summary>Gets or sets an exception to throw from <see cref="Open"/>, or <see langword="null"/> to succeed.</summary>
    public Exception? ThrowOnOpen { get; set; }

    /// <inheritdoc />
    public void Open(string url)
    {
        this.opened.Add(url);

        if (this.ThrowOnOpen is not null)
        {
            throw this.ThrowOnOpen;
        }
    }
}
