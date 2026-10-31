using Agency.Huddle.App.Components.Settings.RemoteAccess;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Manual-run fix (REMOTE-05): a request's ingress reads in words a Human knows, per bind, never as internal vocabulary.</summary>
public sealed class RemoteAccessCopyIngressLabelTests
{
    /// <summary>Each ingress, on each bind and Tailscale mode, has exactly the settled words.</summary>
    [Theory]
    [InlineData("Direct", "lan", "off", "LAN (downgraded)")]
    [InlineData("Direct", "tailnet", "off", "Tailscale address (downgraded)")]
    [InlineData("Direct", "custom", "off", "Network (downgraded)")]
    [InlineData("Managed", "loopback", "serve", "Tailscale")]
    [InlineData("Managed", "loopback", "funnel", "Tailscale Funnel")]
    [InlineData("Loopback", "lan", "off", "This computer")]
    [InlineData("TrustedProxy", "lan", "off", "Proxy")]
    public void IngressLabel_ReadsAsWords(string ingress, string bind, string tailscaleMode, string expected)
    {
        Assert.Equal(expected, RemoteAccessCopy.IngressLabel(ingress, bind, tailscaleMode));
    }
}
