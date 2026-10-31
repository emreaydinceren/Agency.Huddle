using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 8.1 and E-1: <c>TailscalePort = Tailscale:HttpsPort ?? AppPort ?? default</c>; Funnel accepts only 443, 8443 and 10000.</summary>
public sealed class TailscaleHttpsPortTests
{
    /// <summary>A stored port wins over the app port.</summary>
    [Fact]
    public void Resolve_StoredPort_Wins()
    {
        Assert.Equal(8443, TailscaleHttpsPort.Resolve(8443, 5101));
    }

    /// <summary>Without a stored port the app's own fixed port is used, so copies on 5101 and 5102 get separate Tailscale ports (E-1).</summary>
    [Theory]
    [InlineData(5101)]
    [InlineData(5102)]
    public void Resolve_NoStoredPort_UsesTheAppPort(int appPort)
    {
        Assert.Equal(appPort, TailscaleHttpsPort.Resolve(null, appPort));
    }

    /// <summary>With neither, Tailscale's default (443) is used.</summary>
    [Fact]
    public void Resolve_NeitherPort_IsTheDefault()
    {
        Assert.Equal(443, TailscaleHttpsPort.Resolve(null, null));
        Assert.Equal(443, TailscaleHttpsPort.Default);
    }

    /// <summary>Funnel allows exactly 443, 8443 and 10000.</summary>
    [Theory]
    [InlineData(443, true)]
    [InlineData(8443, true)]
    [InlineData(10000, true)]
    [InlineData(5101, false)]
    [InlineData(444, false)]
    [InlineData(80, false)]
    public void FunnelAllows_OnlyTheThreePorts(int port, bool allowed)
    {
        Assert.Equal(allowed, TailscaleHttpsPort.FunnelAllows(port));
    }
}
