using System.Net;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Remote access Phase 0 (Spec 6.4): the shipped <c>AllowedHosts</c> admits only the three loopback host names, so
/// a request for any other <c>Host</c> is answered 400 by the host filter before it reaches the app.
/// </summary>
public sealed class HostFilteringTests
{
    /// <summary>Sends <c>GET /health</c> with the given authority as the <c>Host</c> header, through the real pipeline.</summary>
    private static async Task<HttpStatusCode> GetHealthAsync(string authority, CancellationToken ct)
    {
        using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"http://{authority}/health"));

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        return response.StatusCode;
    }

    /// <summary>A request addressed to a host outside the allowed list is refused with 400.</summary>
    [Fact]
    public async Task Health_ForeignHost_Returns400()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpStatusCode status = await GetHealthAsync("evil.example", ct);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>A foreign host with a port is refused too: the filter ignores the port, not the name.</summary>
    [Fact]
    public async Task Health_ForeignHostWithPort_Returns400()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpStatusCode status = await GetHealthAsync("evil.example:5101", ct);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    /// <summary>The loopback names the installer and a browser use are all admitted, with or without a port.</summary>
    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    [InlineData("localhost:5101")]
    [InlineData("127.0.0.1:5101")]
    [InlineData("[::1]:5101")]
    public async Task Health_LoopbackHost_Returns200(string authority)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpStatusCode status = await GetHealthAsync(authority, ct);

        Assert.Equal(HttpStatusCode.OK, status);
    }
}
