using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The Tailscale part of <see cref="GatewayDynamicHosts"/> (D-7): the MagicDNS name rides on D1's change-token mechanism without clobbering the automatic hosts.</summary>
public sealed class GatewayDynamicHostsTailscaleTests
{
    /// <summary><c>SetTailscale</c> adds hosts to <c>Current</c> and fires the change token.</summary>
    [Fact]
    public void SetTailscale_AddsHostsAndFiresTheToken()
    {
        using GatewayDynamicHosts hosts = new();
        hosts.Set(["desk-a.local"]);
        Microsoft.Extensions.Primitives.IChangeToken token = hosts.GetChangeToken();

        hosts.SetTailscale(["desktop-a.tail1234.ts.net", "desktop-a.tail1234.ts.net:5101"]);

        Assert.True(token.HasChanged);
        Assert.Contains("desk-a.local", hosts.Current);
        Assert.Contains("desktop-a.tail1234.ts.net", hosts.Current);
        Assert.Contains("desktop-a.tail1234.ts.net:5101", hosts.Current);
    }

    /// <summary>Replacing the automatic hosts with <c>Set</c> keeps the Tailscale hosts.</summary>
    [Fact]
    public void Set_KeepsTheTailscaleHosts()
    {
        using GatewayDynamicHosts hosts = new();
        hosts.SetTailscale(["desktop-a.tail1234.ts.net"]);

        hosts.Set(["192.168.1.20"]);

        Assert.Contains("desktop-a.tail1234.ts.net", hosts.Current);
        Assert.Contains("192.168.1.20", hosts.Current);
    }

    /// <summary>Clearing the Tailscale hosts leaves the automatic hosts.</summary>
    [Fact]
    public void SetTailscale_Empty_RemovesOnlyTheTailscaleHosts()
    {
        using GatewayDynamicHosts hosts = new();
        hosts.Set(["192.168.1.20"]);
        hosts.SetTailscale(["desktop-a.tail1234.ts.net"]);

        hosts.SetTailscale([]);

        Assert.DoesNotContain("desktop-a.tail1234.ts.net", hosts.Current);
        Assert.Contains("192.168.1.20", hosts.Current);
    }

    /// <summary>Setting the same Tailscale hosts again does not fire the token (no churn on every poll).</summary>
    [Fact]
    public void SetTailscale_Unchanged_DoesNotFireTheToken()
    {
        using GatewayDynamicHosts hosts = new();
        hosts.SetTailscale(["desktop-a.tail1234.ts.net"]);
        Microsoft.Extensions.Primitives.IChangeToken token = hosts.GetChangeToken();

        hosts.SetTailscale(["desktop-a.tail1234.ts.net"]);

        Assert.False(token.HasChanged);
    }
}
