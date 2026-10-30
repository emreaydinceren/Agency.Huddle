using System.Net;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The automatic host list of Spec 6.4 with correction D-10: bound addresses, <c>{machine}.local</c>, never the bare machine name, plus the configured hosts.</summary>
public sealed class GatewayDynamicHostsTests
{
    /// <summary>The list holds <c>{machine}.local</c> and not the bare machine name.</summary>
    [Fact]
    public void Automatic_HasTheLocalNameAndNotTheBareMachineName()
    {
        IReadOnlyList<string> hosts = GatewayDynamicHosts.Automatic(new GatewayOptions(), "DESK-A", []);

        Assert.Contains("desk-a.local", hosts, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("desk-a", hosts, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Bound addresses are listed, IPv6 bracketed and without a zone.</summary>
    [Fact]
    public void Automatic_ListsBoundAddresses_Ipv6BracketedWithoutZone()
    {
        IReadOnlyList<IPAddress> bound = [IPAddress.Parse("192.168.1.20"), IPAddress.Parse("fd00::5%3")];

        IReadOnlyList<string> hosts = GatewayDynamicHosts.Automatic(new GatewayOptions(), "desk-a", bound);

        Assert.Contains("192.168.1.20", hosts);
        Assert.Contains("[fd00::5]", hosts);
    }

    /// <summary><c>CustomBindHost</c> and <c>ExtraHosts</c> are listed.</summary>
    [Fact]
    public void Automatic_ListsCustomBindHostAndExtraHosts()
    {
        GatewayOptions options = new() { CustomBindHost = "192.168.1.30", ExtraHosts = { "files.example.net" } };

        IReadOnlyList<string> hosts = GatewayDynamicHosts.Automatic(options, "desk-a", []);

        Assert.Contains("192.168.1.30", hosts);
        Assert.Contains("files.example.net", hosts);
    }
}
