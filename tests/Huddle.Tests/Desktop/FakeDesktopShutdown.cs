using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>An <see cref="IDesktopShutdown"/> that records how many times a stop was requested.</summary>
internal sealed class FakeDesktopShutdown : IDesktopShutdown
{
    /// <summary>Gets the number of times <see cref="Stop"/> was called.</summary>
    public int StopCalls { get; private set; }

    /// <summary>Records the stop request instead of stopping anything.</summary>
    public void Stop()
    {
        this.StopCalls++;
    }
}
