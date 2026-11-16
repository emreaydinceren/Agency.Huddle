using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.9 and C-9: <c>POST /_desktop/bootstrap</c> on the real pipeline, loopback with the instance token, desktop mode only.</summary>
public sealed partial class DesktopBootstrapEndpointTests
{
    [GeneratedRegex(@"^http://localhost(:\d+)?/auth\?return=%2F#bootstrapToken=(?<token>[A-Za-z0-9_-]{43})$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlShape();

    private static HttpRequestMessage Post(string? token, string? ip = null)
    {
        HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Post, "/_desktop/bootstrap", ip: ip);
        if (token is not null)
        {
            request.Headers.Add(DesktopStopEndpoint.TokenHeader, token);
        }

        return request;
    }

    /// <summary>Loopback plus the instance token answers 200 <c>{ "url": ... }</c> on <c>/auth</c>, and the token in it redeems as admin.</summary>
    [Fact]
    public async Task Post_InstanceToken_Returns200WithTheAuthUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string instanceToken = host.Services.GetRequiredService<DesktopSession>().StopToken;

        using HttpResponseMessage response = await host.Client.SendAsync(Post(instanceToken), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        string? url = json.RootElement.GetProperty("url").GetString();
        Assert.NotNull(url);
        Match match = UrlShape().Match(url);
        Assert.True(match.Success, url);
        BootstrapTokenStore.Redemption redemption = host.Services.GetRequiredService<BootstrapTokenStore>().Redeem(match.Groups["token"].Value, "dev-a");
        Assert.True(redemption.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.Admin, redemption.TokenKind);
    }

    /// <summary>A missing or wrong instance token is 401.</summary>
    /// <param name="token">The presented token, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("wrong-token")]
    public async Task Post_MissingOrWrongToken_Returns401(string? token)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(Post(token), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A non-loopback caller is 403 even with the right token.</summary>
    [Fact]
    public async Task Post_NonLoopback_Returns403()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string instanceToken = host.Services.GetRequiredService<DesktopSession>().StopToken;

        using HttpResponseMessage response = await host.Client.SendAsync(Post(instanceToken, GatewayGateHost.LanAddress), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Outside desktop mode the route does not exist (the Razor catch-all answers 404 or 405, never the mint).</summary>
    [Fact]
    public async Task Post_NotDesktopMode_IsNotFoundOrNotAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/_desktop/bootstrap", UriKind.Relative));
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, "anything");

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
    }
}
