using System.Net;
using System.Net.Sockets;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>C-11: in desktop mode the first bound port is stored in <c>gateway_settings.desktop_port</c> and requested next time; a busy port falls back to 0.</summary>
public sealed class DesktopPortChoiceTests : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>With nothing stored, the choice is 0 (any port) and the probe is not consulted.</summary>
    [Fact]
    public void Choose_NothingStored_IsZero()
    {
        Assert.Equal(0, DesktopPortChoice.Choose(null, _ => throw new InvalidOperationException("not consulted")));
    }

    /// <summary>A stored port that is free is requested again.</summary>
    [Fact]
    public void Choose_StoredAndFree_IsTheStoredPort()
    {
        Assert.Equal(5123, DesktopPortChoice.Choose(5123, port => port == 5123));
    }

    /// <summary>A stored port that is busy falls back to 0.</summary>
    [Fact]
    public void Choose_StoredAndBusy_IsZero()
    {
        Assert.Equal(0, DesktopPortChoice.Choose(5123, _ => false));
    }

    /// <summary>A stored value that is not a usable port is ignored without a probe.</summary>
    /// <param name="stored">The stored value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(65536)]
    public void Choose_InvalidStoredPort_IsZero(int stored)
    {
        Assert.Equal(0, DesktopPortChoice.Choose(stored, _ => throw new InvalidOperationException("not consulted")));
    }

    /// <summary>
    /// The real probe says busy while a listener holds the port and free once it is released. Another process may take a released
    /// port before the second probe, so a fresh port is tried a few times; a probe that never reports free still fails.
    /// </summary>
    [Fact]
    public void IsFree_ReflectsALiveListener()
    {
        bool freeOnceReleased = false;
        for (int attempt = 0; attempt < 5 && !freeOnceReleased; attempt++)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Assert.False(DesktopPortChoice.IsFree(port));
            listener.Stop();

            freeOnceReleased = DesktopPortChoice.IsFree(port);
        }

        Assert.True(freeOnceReleased);
    }

    /// <summary>A fresh database has no desktop port.</summary>
    [Fact]
    public void Store_Fresh_HasNoDesktopPort()
    {
        GatewaySettingsStore store = new(this.dir.Path);

        Assert.Null(store.DesktopPort);
    }

    /// <summary>The port round-trips, survives a new store over the same directory, and can be replaced.</summary>
    [Fact]
    public void Store_SetDesktopPort_RoundTripsAndPersists()
    {
        GatewaySettingsStore store = new(this.dir.Path);

        store.SetDesktopPort(5123);

        Assert.Equal(5123, store.DesktopPort);
        Assert.Equal(5123, new GatewaySettingsStore(this.dir.Path).DesktopPort);
        store.SetDesktopPort(5999);
        Assert.Equal(5999, new GatewaySettingsStore(this.dir.Path).DesktopPort);
    }

    /// <summary>The first start stores the port it got; the next start requests it; when it is busy the new port replaces it.</summary>
    [Fact]
    public void Remember_StoresTheBoundPortAndTheNextChoiceUsesIt()
    {
        GatewaySettingsStore store = new(this.dir.Path);
        Assert.Equal(0, DesktopPortChoice.Choose(store.DesktopPort, _ => true));

        DesktopPortChoice.Remember(store, 5123);

        Assert.Equal(5123, store.DesktopPort);
        Assert.Equal(5123, DesktopPortChoice.Choose(store.DesktopPort, _ => true));
        Assert.Equal(0, DesktopPortChoice.Choose(store.DesktopPort, _ => false));
        DesktopPortChoice.Remember(store, 6001);
        Assert.Equal(6001, store.DesktopPort);
    }
}
