using System.Net;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The plain-HTTP rule of Spec 8.8 with correction D-10, wired into the gate: an http request with a bare machine-name <c>Host</c> is refused unless the admin listed it.</summary>
public sealed class GatewayPlainHttpGateTests
{
    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(url));
        return await client.SendAsync(request, ct);
    }

    /// <summary>A plain-HTTP request whose <c>Host</c> is a bare machine name is refused 403 with an explanatory page; the same request over https and a <c>.local</c> name pass.</summary>
    [Fact]
    public async Task Gate_PlainHttpWithBareMachineName_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new();
        using HttpClient client = host.CreateClient(managed: false, forwarded: false);

        using HttpResponseMessage bare = await GetAsync(client, "http://desk-a/auth/challenge", ct);
        using HttpResponseMessage secure = await GetAsync(client, "https://desk-a/auth/challenge", ct);
        using HttpResponseMessage local = await GetAsync(client, "http://desk-a.local/auth/challenge", ct);

        Assert.Equal(HttpStatusCode.Forbidden, bare.StatusCode);
        Assert.Equal("text/html", bare.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
    }

    /// <summary>A bare name the admin listed in <c>ExtraHosts</c> passes over plain http (labelled downgraded elsewhere).</summary>
    [Fact]
    public async Task Gate_PlainHttpWithAnExtraHostsName_Passes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ManagedIngressHost host = new(new Dictionary<string, string> { ["Team:Gateway:ExtraHosts:0"] = "desk-a" });
        using HttpClient client = host.CreateClient(managed: false, forwarded: false);

        using HttpResponseMessage response = await GetAsync(client, "http://desk-a/auth/challenge", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
