using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Teams;

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
    [InlineData("/teams/Business", "Business — Huddle", "Members", ".team-members")]
    [InlineData("/teams/Business/files", "Business — Huddle", "Files", ".team-files-tab")]
    [InlineData("/teams/Business/projects/Marketing%20Project", "Marketing Project · Business — Huddle", "Files", ".team-files-tab")]
    [InlineData("/teams/Business/projects/Marketing%20Project/tasks", "Marketing Project · Business — Huddle", "Tasks", ".team-tasks-tab")]
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

    /// <summary>The Team page's prerendered title is <c>Business — Huddle</c>, the Team's own spelling and no Project.</summary>
    [Fact]
    public async Task TeamPage_Title_IsTheTeamNameThenHuddle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Business", ct);

        Assert.Equal("Business — Huddle", TitleOf(page));
    }

    /// <summary>E-11: <c>/teams/Sales%20%26%20Ops</c> resolves the Team <c>Sales &amp; Ops</c> (title and default tab) and its own links are escaped.</summary>
    [Fact]
    public async Task EscapedTeamRoute_ResolvesTheTeamWithAmpersand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Sales%20%26%20Ops", ct);

        Assert.Equal("Sales & Ops — Huddle", TitleOf(page));
        Assert.Equal("Members", TextOf(page, ActiveTabSelector));
    }

    /// <summary>E-11: on a Project page the Team crumb's href carries the escaped label, <c>/teams/Sales%20%26%20Ops</c>.</summary>
    [Fact]
    public async Task EscapedProjectRoute_BreadcrumbLinksToTheEscapedTeamHref()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/Sales%20%26%20Ops/projects/Launch", ct);

        IElement? crumb = page.QuerySelector(".team-page-breadcrumbs a[href]");
        Assert.NotNull(crumb);
        Assert.Equal("/teams/Sales%20%26%20Ops", crumb.GetAttribute("href"));
        Assert.Equal("Sales & Ops", crumb.TextContent.Trim());
        Assert.Equal("Launch · Sales & Ops — Huddle", TitleOf(page));
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

    /// <summary>A lowercase route <c>/teams/business</c> shows the catalog's display spelling <c>Business</c> in the title and the breadcrumb.</summary>
    [Fact]
    public async Task LowercaseTeamRoute_ShowsTheDisplaySpelling()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, ct);

        IHtmlDocument page = await GetPageAsync(client, "/teams/business", ct);

        Assert.Equal("Business — Huddle", TitleOf(page));
        Assert.Equal("Business", TextOf(page, ".team-page-breadcrumbs"));
    }

    /// <summary>Seeds the Persona and the Project folders, creates the client, and waits until the host's catalog lists all three Projects.</summary>
    private static async Task<HttpClient> StartAsync(TeamWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        await factory.WriteDefinitionAsync("nova", PersonaText, cancellationToken);
        Directory.CreateDirectory(Path.Combine(factory.TasksDirPath, "Business", "Marketing Project"));
        Directory.CreateDirectory(Path.Combine(factory.TasksDirPath, "Sales & Ops", "Launch"));

        HttpClient client = factory.CreateClient();
        ITeamCatalog catalog = factory.Services.GetRequiredService<ITeamCatalog>();
        await WaitForAsync(
            () => catalog.Find("Business")?.Projects.Contains("Marketing Project") == true
                && catalog.Find("Sales & Ops")?.Projects.Contains("Launch") == true,
            cancellationToken);
        return client;
    }

    /// <summary>GETs <paramref name="path"/>, asserts a 200, and parses the body.</summary>
    private static async Task<IHtmlDocument> GetPageAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        string html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new HtmlParser().ParseDocument(html);
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

    /// <summary>Polls <paramref name="condition"/> until it is true or ten seconds pass, for a watcher-driven catalog without a bare delay.</summary>
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the catalog to list the seeded Teams and Projects.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
