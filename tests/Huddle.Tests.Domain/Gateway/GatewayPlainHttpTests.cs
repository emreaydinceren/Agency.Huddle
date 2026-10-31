using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The plain-HTTP rule of Spec 8.8 with correction D-10: local and private hosts only, unless the admin named the host.</summary>
public sealed class GatewayPlainHttpTests
{
    /// <summary>Loopback names, <c>*.ts.net</c>, <c>*.local</c> and private IP literals pass over http.</summary>
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    [InlineData("desk.tail1.ts.net")]
    [InlineData("desk.local")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.20")]
    [InlineData("172.16.0.9")]
    [InlineData("100.101.102.103")]
    public void IsAllowed_LocalAndPrivateHosts_PassOverHttp(string host)
    {
        Assert.True(GatewayPlainHttp.IsAllowed("http", host, new GatewayOptions()));
    }

    /// <summary>A bare machine name, a public name and a public literal are refused over http (D-10: the bare name is not automatic).</summary>
    [Theory]
    [InlineData("desk-a")]
    [InlineData("example.com")]
    [InlineData("8.8.8.8")]
    [InlineData("xts.net")]
    public void IsAllowed_OtherHosts_RefusedOverHttp(string host)
    {
        Assert.False(GatewayPlainHttp.IsAllowed("http", host, new GatewayOptions()));
    }

    /// <summary>https passes for any host.</summary>
    [Fact]
    public void IsAllowed_Https_PassesForAnyHost()
    {
        Assert.True(GatewayPlainHttp.IsAllowed("https", "example.com", new GatewayOptions()));
    }

    /// <summary>A host the admin listed in <c>ExtraHosts</c> or <c>CustomBindHost</c> is exempt, compared ignoring case.</summary>
    [Fact]
    public void IsAllowed_ExtraHostsAndCustomBindHost_AreExempt()
    {
        GatewayOptions options = new() { ExtraHosts = { "Desk-A" }, CustomBindHost = "files.example.net" };

        Assert.True(GatewayPlainHttp.IsAllowed("http", "desk-a", options));
        Assert.True(GatewayPlainHttp.IsAllowed("http", "FILES.example.net", options));
        Assert.False(GatewayPlainHttp.IsAllowed("http", "other.example.net", options));
    }
}
