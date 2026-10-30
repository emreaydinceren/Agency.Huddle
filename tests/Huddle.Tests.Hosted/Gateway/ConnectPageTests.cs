using System.Net;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Gateway;

/// <summary><c>GET /auth</c> (Spec 6.6, "the connect page"): a small HTML page that is not a Blazor page.</summary>
public sealed class ConnectPageTests
{
    /// <summary>The page is HTML, is titled "Connect to Huddle", loads <c>auth.js</c> and is never cached.</summary>
    [Fact]
    public async Task Get_Auth_ServesTheConnectPage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/auth", UriKind.Relative), ct);
        string html = await response.Content.ReadAsStringAsync(ct);
        int titleStart = html.IndexOf("<title>", StringComparison.Ordinal) + "<title>".Length;
        string title = html[titleStart..html.IndexOf("</title>", StringComparison.Ordinal)];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Connect to Huddle", title);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(1, html.Split("/auth.js", StringSplitOptions.None).Length - 1);
    }
}
