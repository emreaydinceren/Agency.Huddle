using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins the HTTP half of Spec §6.6 for <c>TeamPage</c> (Task 7.13b): bUnit has no Router, so the four
/// routes, the escaping of a Team label such as <c>Sales &amp; Ops</c> (E-11), the unknown-Team notice and
/// the prerendered <c>PageTitle</c> are read from a plain GET against <see cref="TeamWebApplicationFactory"/>.
/// Every test seeds a Persona whose <c>Teams:</c> line names the Teams and creates the Project folders
/// before <c>CreateClient()</c>, then waits for the host's catalog to list them.
/// </summary>
public sealed class TeamPageRouteTests
{
    private const string PersonaText = "---\nName: Nova\nTitle: Nova\nAlias: nova\nTeams: Business, Sales & Ops\n---\nYou are Nova.";

    private const string ActiveTabSelector = ".team-page-tabs .mud-tab-active";

    /// <summary>Each of the four routes answers 200 with its own prerendered title and opens its default (or named) tab.</summary>
    [Theory]
    [InlineData("/teams/Business", "Business", "Members", ".team-roster")]
    [InlineData("/teams/Business/files", "Business", "Files", ".team-files-tab")]
    [InlineData("/teams/Business/projects/Marketing%20Project", "Marketing Project", "Files", ".team-files-tab")]
    [InlineData("/teams/Business/projects/Marketing%20Project/tasks", "Marketing Project", "Tasks", ".team-tasks-tab")]
    public async Task Route_Returns200_WithTheTitleAndTheActiveTab(string path, string expectedTitle, string expectedTab, string expectedPanel)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, path, ct);

        Assert.Equal(expectedTitle, TitleOf(page));
        Assert.Equal(expectedTab, TextOf(page, ActiveTabSelector));
        Assert.Single(page.QuerySelectorAll(expectedPanel));
    }

    /// <summary>The Team page's prerendered title is <c>Business</c>, the Team's own spelling: the bare page name every page uses.</summary>
    [Fact]
    public async Task TeamPage_Title_IsTheBareTeamName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Business", ct);

        Assert.Equal("Business", TitleOf(page));
    }

    /// <summary>
    /// Reading the body after the response has completed must still work. The page's components are disposed when the response
    /// completes; if one throws there, TestHost aborts the body stream and a late read sees a cancellation. Delaying the read makes
    /// that deterministic instead of a race against a loaded runner. It found <c>BuildTeam.Dispose</c> throwing
    /// <see cref="ObjectDisposedException"/> by resolving a service from the request scope that was already being disposed.
    /// </summary>
    [Fact]
    public async Task TeamPage_BodyReadAfterTheResponseCompleted_StillSucceeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        using HttpResponseMessage response = await client.GetAsync("/teams/Business", HttpCompletionOption.ResponseHeadersRead, ct);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        string html;
        try
        {
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw NamedFailure("/teams/Business", ex);
        }

        Assert.Equal("Business", TitleOf(new HtmlParser().ParseDocument(html)));
        // The log is process wide and other hosted tests run in parallel, so only a throw while the renderer disposes the page counts.
        List<string> disposeFailures = [.. HostingExceptionLog.Snapshot().Where(static entry => entry.Contains("Renderer.Dispose", StringComparison.Ordinal))];
        Assert.True(disposeFailures.Count == 0, string.Join(Environment.NewLine + "----" + Environment.NewLine, disposeFailures));
    }

    /// <summary>E-11: <c>/teams/Sales%20%26%20Ops</c> resolves the Team <c>Sales &amp; Ops</c> (title and default tab) and its own links are escaped.</summary>
    [Fact]
    public async Task EscapedTeamRoute_ResolvesTheTeamWithAmpersand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Sales%20%26%20Ops", ct);

        Assert.Equal("Sales & Ops", TitleOf(page));
        Assert.Equal("Members", TextOf(page, ActiveTabSelector));
    }

    /// <summary>E-11: on a Project page the shell's Team crumb carries the escaped label in its href, <c>/teams/Sales%20%26%20Ops</c>.</summary>
    [Fact]
    public async Task EscapedProjectRoute_BreadcrumbLinksToTheEscapedTeamHref()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Sales%20%26%20Ops/projects/Launch", ct);

        IElement? crumb = page.QuerySelector(".page-breadcrumbs a[href]");
        Assert.NotNull(crumb);
        Assert.Equal("/teams/Sales%20%26%20Ops", crumb.GetAttribute("href"));
        Assert.Equal("Sales & Ops", crumb.TextContent.Trim());
        Assert.Equal("Launch", TitleOf(page));
    }

    /// <summary>An unknown Team is a 200 page with the notice text and creates no folder under <c>Teams/</c>.</summary>
    [Fact]
    public async Task UnknownTeam_ShowsTheNotice_AndCreatesNoFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Nope", ct);

        Assert.Equal("There is no Team named \"Nope\".", TextOf(page, ".team-page-notice-text"));
        Assert.False(Directory.Exists(Path.Combine(factory.TasksDirPath, "Nope")));
    }

    /// <summary>A lowercase route <c>/teams/business</c> shows the catalog's display spelling <c>Business</c> in the page title.</summary>
    [Fact]
    public async Task LowercaseTeamRoute_ShowsTheDisplaySpelling()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/business", ct);

        Assert.Equal("Business", TitleOf(page));
    }

    /// <summary>Seeds the Persona and the Project folders, creates the client, and waits until the host's catalog lists all three Projects.</summary>
    private static async Task<HttpClient> StartAsync(TeamWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        await factory.WriteDefinitionAsync("nova", PersonaText, cancellationToken);
        Directory.CreateDirectory(Path.Combine(factory.TasksDirPath, "Business", "Marketing Project"));
        Directory.CreateDirectory(Path.Combine(factory.TasksDirPath, "Sales & Ops", "Launch"));

        HostingExceptionLog.Start();
        HttpClient client = factory.CreateClient();
        ITeamCatalog catalog = factory.Services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find("Business")?.Projects.Contains("Marketing Project") == true
                && catalog.Find("Sales & Ops")?.Projects.Contains("Launch") == true, cancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        return client;
    }

    /// <summary>GETs <paramref name="path"/>, asserts a 200, and parses the body.</summary>
    private static async Task<IHtmlDocument> GetPageAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
            string html = await response.Content.ReadAsStringAsync(cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return new HtmlParser().ParseDocument(html);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw NamedFailure(path, ex);
        }
    }

    /// <summary>
    /// TestHost reports a server-side exception after the response was written as a bare cancellation and drops the exception.
    /// This turns it into a failure that names what Hosting recorded, so a flaky run says which component threw.
    /// </summary>
    private static InvalidOperationException NamedFailure(string path, OperationCanceledException cancellation)
    {
        IReadOnlyList<string> recorded = HostingExceptionLog.Snapshot();
        return new InvalidOperationException(
            $"GET {path} was cancelled by the server, not the test. Hosting recorded {recorded.Count} unhandled exception(s):{Environment.NewLine}{string.Join(Environment.NewLine + "----" + Environment.NewLine, recorded)}",
            cancellation);
    }

    /// <summary>The prerendered document title, trimmed.</summary>
    private static string TitleOf(IHtmlDocument page) => TextOf(page, "title");

    /// <summary>The trimmed text of the single element matching <paramref name="selector"/>.</summary>
    private static string TextOf(IHtmlDocument page, string selector)
    {
        IElement? element = page.QuerySelector(selector);
        Assert.NotNull(element);
        return element.TextContent.Trim();
    }

}
