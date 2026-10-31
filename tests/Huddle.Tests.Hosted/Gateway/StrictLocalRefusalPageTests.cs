using System.Net;
using System.Text.Json;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The strict local refusal as a browser sees it: a page that says what to do when the request is a GET or HEAD that accepts HTML,
/// and the unchanged JSON <c>strict_local</c> body for every other request.
/// </summary>
public sealed class StrictLocalRefusalPageTests : IDisposable
{
    private const string Paragraph = "This Huddle only accepts connections from the computer it runs on. To use it from this device, turn off Strict local in Settings › Remote access on that computer, then restart Huddle.";

    private readonly TeamWebApplicationFactory root = new() { ConnectedAsAdmin = false };
    private readonly List<WebApplicationFactory<Program>> apps = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (WebApplicationFactory<Program> app in this.apps)
        {
            app.Dispose();
        }

        this.root.Dispose();
    }

    private HttpClient Client()
    {
        WebApplicationFactory<Program> app = this.root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Gateway:Auth:Token", "0123456789abcdef0123456789abcdef0123456789abcdef");
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestRemoteAddressStartupFilter>());
        });
        this.apps.Add(app);
        return app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    /// <summary>A browser GET from a LAN client gets a 403 HTML page with the exact title, heading and paragraph, and no scripts or external files.</summary>
    [Fact]
    public async Task LanBrowserGet_GetsTheReadablePage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/", null, GatewayGateHost.LanAddress, html: true);

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        IHtmlDocument page = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("Huddle is local only", page.Title);
        Assert.Equal("Huddle is local only", page.QuerySelector("h1")?.TextContent.Trim());
        Assert.Equal(Paragraph, page.QuerySelector("p")?.TextContent.Trim());
        Assert.Empty(page.QuerySelectorAll("script, link, img, iframe"));
    }

    /// <summary>A request that does not accept HTML keeps the JSON body with code <c>strict_local</c>.</summary>
    [Fact]
    public async Task LanNonHtmlGet_KeepsTheJsonCode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/auth/status", null, GatewayGateHost.LanAddress, html: false);

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("strict_local", body.RootElement.GetProperty("code").GetString());
    }

    /// <summary>A POST that accepts HTML still gets the JSON body: the page is for navigation only.</summary>
    [Fact]
    public async Task LanHtmlPost_KeepsTheJsonCode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient client = this.Client();
        using HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Post, "/auth/connect", null, GatewayGateHost.LanAddress, html: true);

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("strict_local", body.RootElement.GetProperty("code").GetString());
    }
}
