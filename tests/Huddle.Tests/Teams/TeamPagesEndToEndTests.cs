using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 8.1.t (Spec §13, §2 T0-T15): the delivered Team-page parts walked together through the real host
/// (<see cref="TeamWebApplicationFactory"/>, a temp data directory, Personas <c>Nova</c> and <c>Kim</c>, no
/// ACP). Each test seeds its own host through <see cref="StartAsync"/> and follows one numbered step of the
/// deliverable: folders and catalog, membership, Team Memory in the prompt, the reserved <c>memory</c> name,
/// the two pages, and the two catalog flags. The host's catalog lags disk changes by about 500 ms, so every
/// read of it waits with <see cref="WaitForAsync"/> instead of a bare delay.
/// </summary>
public sealed class TeamPagesEndToEndTests
{
    private const string NovaText = "---\nName: Nova\nTitle: Nova\nAlias: nova\n---\nYou are Nova.";

    private const string KimText = "---\nName: Kim\nTitle: Kim\nAlias: kim\n---\nYou are Kim.";

    /// <summary>A Team label <c>TeamFolderProvisioner</c> refuses (a name may not contain a question mark), so it never gets a folder. The provisioner creates one for every ordinary label at host start and on every Personas change, so an ordinary label's "no folder" state is only ever transient.</summary>
    private const string OpsLabel = "Ops?";

    private const string LaunchLine = "The campaign launches on 3 November.";

    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    /// <summary>Step 1: <c>EnsureTeam</c> and <c>EnsureProjectIn</c> succeed and the catalog lists the Team with its one Project.</summary>
    [Fact]
    public async Task EnsureTeamAndProject_Succeed_AndTheCatalogListsBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, NovaText, KimText, ct);
        ITeamFolders folders = factory.Services.GetRequiredService<ITeamFolders>();
        ITeamCatalog catalog = factory.Services.GetRequiredService<ITeamCatalog>();

        LibraryResult<LibraryPath> team = folders.EnsureTeam("Business");
        await WaitForAsync(() => catalog.Find("Business") is not null, ct);
        LibraryResult<LibraryPath> project = folders.EnsureProjectIn("Business", "Marketing Project");
        await WaitForAsync(() => catalog.Find("Business")?.Projects.Contains("Marketing Project") == true, ct);

        Assert.Null(team.Error);
        Assert.Null(project.Error);
        Assert.True(Directory.Exists(Path.Combine(factory.TasksDirPath, "Business", "Marketing Project")));
        TeamSummary? business = catalog.Find("Business");
        Assert.NotNull(business);
        Assert.Equal("Business", business.Name);
        Assert.Equal(["Marketing Project"], business.Projects);
        Assert.True(business.HasFolder);
        Assert.False(business.HasMembers);
    }

    /// <summary>Step 2: adding Kim to Business returns <c>Added</c>, the catalog lists Kim, and Kim's definition file carries <c>Business</c> in <c>teams</c>.</summary>
    [Fact]
    public async Task AddMember_ReturnsAdded_TheCatalogListsKim_AndTheDefinitionFileHoldsTheLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, NovaText, KimText, ct);
        ITeamCatalog catalog = await SeedBusinessAsync(factory, ct);
        ITeamMembership membership = factory.Services.GetRequiredService<ITeamMembership>();

        MembershipResult result = membership.Add("Business", "Kim");
        await WaitForAsync(() => catalog.Find("Business")?.Members.Contains("Kim") == true, ct);

        Assert.Equal(MembershipOutcome.Added, result.Outcome);
        Assert.Null(result.Problem);
        Assert.Equal(["Kim"], catalog.Find("Business")?.Members);
        string definition = await File.ReadAllTextAsync(Path.Combine(factory.TeammatesDirPath, "kim", "kim.md"), ct);
        Assert.True(PersonaFrontmatter.TryReadIdentity(definition, out PersonaIdentity? identity, out string error), error);
        Assert.Equal(["Business"], identity.Teams);
    }

    /// <summary>Step 3: Kim's Team Memory index and composed prompt carry the Project entry under <c>Business › Marketing Project:</c>; Nova, who is not in Business, gets no block.</summary>
    [Fact]
    public async Task TeamMemory_ReachesKimsPrompt_AndNotANonMembers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, NovaText, KimText, ct);
        ITeamCatalog catalog = await SeedBusinessAsync(factory, ct);
        ITeamMembership membership = factory.Services.GetRequiredService<ITeamMembership>();
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        IPromptSource prompts = factory.Services.GetRequiredService<IPromptSource>();
        Assert.Equal(MembershipOutcome.Added, membership.Add("Business", "Kim").Outcome);
        await WaitForAsync(() => catalog.Find("Business")?.Members.Contains("Kim") == true, ct);
        string memoryDir = Path.Combine(factory.TasksDirPath, "Business", "Marketing Project", "memory");
        Directory.CreateDirectory(memoryDir);
        string launchFile = Path.Combine(memoryDir, "launch-date.md");
        await File.WriteAllTextAsync(launchFile, LaunchLine, ct);

        Persona? kim = personas.Get("Kim");
        Persona? nova = personas.Get("Nova");
        Assert.NotNull(kim);
        Assert.NotNull(nova);
        Assert.True(PersonaFrontmatter.TryReadIdentity(kim.Text, out PersonaIdentity? kimIdentity, out string error), error);
        Assert.True(PersonaFrontmatter.TryReadIdentity(nova.Text, out PersonaIdentity? novaIdentity, out error), error);
        TeamMemorySnapshot kimSnapshot = TeamMemoryIndex.Build(factory.TasksDirPath, kimIdentity.Teams, catalog.Teams, 50);
        TeamMemorySnapshot novaSnapshot = TeamMemoryIndex.Build(factory.TasksDirPath, novaIdentity.Teams, catalog.Teams, 50);

        TeamMemoryGroup group = Assert.Single(kimSnapshot.Groups);
        Assert.Equal("Business", group.Team);
        Assert.Empty(group.TeamWide.Entries);
        (string project, MemorySnapshot memory) = Assert.Single(group.Projects);
        Assert.Equal("Marketing Project", project);
        Assert.Equal([new MemoryEntry(LaunchLine, launchFile)], memory.Entries);
        string kimPrompt = Compose(kim, prompts, kimSnapshot);
        string novaPrompt = Compose(nova, prompts, novaSnapshot);
        // contains-ok: the Team Memory block sits inside a larger composed system prompt.
        Assert.Contains($"Business › Marketing Project:\n- {LaunchLine} ({launchFile})", kimPrompt, StringComparison.Ordinal);
        Assert.Empty(novaSnapshot.Groups);
        Assert.DoesNotContain("## Team Memory", novaPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(LaunchLine, novaPrompt, StringComparison.Ordinal);
    }

    /// <summary>Step 4: <c>memory</c> is refused as a Project name and creates nothing; a Task under <c>Business/memory/_tasks/</c> is not loaded.</summary>
    [Fact]
    public async Task ReservedMemoryName_IsRefused_AndAStrandedTaskIsNotLoaded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, NovaText, KimText, ct);
        ITeamCatalog catalog = await SeedBusinessAsync(factory, ct);
        ITeamFolders folders = factory.Services.GetRequiredService<ITeamFolders>();
        TaskStore tasks = factory.Services.GetRequiredService<TaskStore>();
        string businessDir = Path.Combine(factory.TasksDirPath, "Business");

        LibraryResult<LibraryPath> result = folders.EnsureProjectIn("Business", "memory");

        Assert.Null(result.Value);
        Assert.Equal("\"memory\" is reserved for the Team's shared Memory.", result.Error);
        Assert.Equal(TeamNames.MemoryReservedProblem, result.Error);
        Assert.False(Directory.Exists(Path.Combine(businessDir, "memory")));
        Assert.Equal(
            ["Marketing Project"],
            Directory.GetDirectories(businessDir).Select(static dir => Path.GetFileName(dir)).Order(StringComparer.Ordinal).ToList());

        _ = TestTaskStore.WriteTask(
            factory.TasksDirPath,
            Path.Combine("Business", "memory", "_tasks", "BUS-0002.md"),
            TestTasks.Make(id: "BUS-0002", location: new("Business", "memory", false)));
        _ = TestTaskStore.WriteTask(
            factory.TasksDirPath,
            Path.Combine("Business", "_tasks", "BUS-0001.md"),
            TestTasks.Make(id: "BUS-0001", location: new("Business", null, false)));
        await WaitForAsync(() => tasks.All.Any(static task => task.Id.ToString() == "BUS-0001"), ct);

        Assert.Equal(["BUS-0001"], tasks.All.Select(static task => task.Id.ToString()).ToList());
        Assert.Equal(["Marketing Project"], catalog.Find("Business")?.Projects);
    }

    /// <summary>Step 5: the Team page and the Project page each answer 200 and their breadcrumb reads the Team, then the Team and Project.</summary>
    [Fact]
    public async Task TeamAndProjectPages_Return200_WithTheirBreadcrumbs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = await StartAsync(factory, NovaText, KimText, ct);
        _ = await SeedBusinessAsync(factory, ct);

        IHtmlDocument teamPage = await GetPageAsync(client, "/teams/Business", ct);
        IHtmlDocument projectPage = await GetPageAsync(client, "/teams/Business/projects/Marketing%20Project", ct);

        Assert.Equal("Business", TextOf(teamPage, ".team-page-breadcrumbs"));
        Assert.Equal("Business › Marketing Project", NormalisedTextOf(projectPage, ".team-page-breadcrumbs"));
        Assert.Equal("/teams/Business", projectPage.QuerySelector(".team-page-breadcrumbs a[href]")?.GetAttribute("href"));
    }

    /// <summary>Step 6 (T14, T15): a folder <c>Legal</c> nobody carries has no members; a label the folder provisioner cannot turn into a folder has no folder.</summary>
    [Fact]
    public async Task CatalogFlags_FolderWithoutMembers_AndLabelWithoutFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string opsNova = "---\nName: Nova\nTitle: Nova\nAlias: nova\nTeams: " + OpsLabel + "\n---\nYou are Nova.";
        await using TeamWebApplicationFactory factory = new();
        Directory.CreateDirectory(Path.Combine(factory.TasksDirPath, "Legal"));
        using HttpClient client = await StartAsync(factory, opsNova, KimText, ct);
        ITeamCatalog catalog = factory.Services.GetRequiredService<ITeamCatalog>();

        await WaitForAsync(() => catalog.Find("Legal") is not null && catalog.Find(OpsLabel) is not null, ct);

        TeamSummary? legal = catalog.Find("Legal");
        TeamSummary? ops = catalog.Find(OpsLabel);
        Assert.NotNull(legal);
        Assert.NotNull(ops);
        Assert.True(legal.HasFolder);
        Assert.False(legal.HasMembers);
        Assert.Empty(legal.Members);
        Assert.False(ops.HasFolder);
        Assert.True(ops.HasMembers);
        Assert.Equal(["Nova"], ops.Members);
        Assert.Equal(["Legal", OpsLabel], catalog.Teams.Select(static team => team.Name).ToList());
    }

    /// <summary>Seeds the two Personas, starts the host and waits until the Personas are loaded.</summary>
    private static async Task<HttpClient> StartAsync(TeamWebApplicationFactory factory, string novaText, string kimText, CancellationToken cancellationToken)
    {
        await factory.WriteDefinitionAsync("nova", novaText, cancellationToken);
        await factory.WriteDefinitionAsync("kim", kimText, cancellationToken);

        HttpClient client = factory.CreateClient();
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        await WaitForAsync(() => personas.Get("Nova") is not null && personas.Get("Kim") is not null, cancellationToken);
        return client;
    }

    /// <summary>Creates <c>Business</c> and its <c>Marketing Project</c> through <c>ITeamFolders</c> and waits for the catalog to list them.</summary>
    private static async Task<ITeamCatalog> SeedBusinessAsync(TeamWebApplicationFactory factory, CancellationToken cancellationToken)
    {
        ITeamFolders folders = factory.Services.GetRequiredService<ITeamFolders>();
        ITeamCatalog catalog = factory.Services.GetRequiredService<ITeamCatalog>();
        Assert.Null(folders.EnsureTeam("Business").Error);
        await WaitForAsync(() => catalog.Find("Business") is not null, cancellationToken);
        Assert.Null(folders.EnsureProjectIn("Business", "Marketing Project").Error);
        await WaitForAsync(() => catalog.Find("Business")?.Projects.Contains("Marketing Project") == true, cancellationToken);
        return catalog;
    }

    /// <summary>Composes one Persona's system prompt with the given Team Memory snapshot.</summary>
    private static string Compose(Persona persona, IPromptSource prompts, TeamMemorySnapshot snapshot)
    {
        return SystemPromptComposer.Compose(
            persona,
            prompts,
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            null,
            SessionScope.Shared,
            teamMemory: snapshot);
    }

    /// <summary>GETs <paramref name="path"/>, asserts a 200, and parses the body.</summary>
    private static async Task<IHtmlDocument> GetPageAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path, cancellationToken);
        string html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new HtmlParser().ParseDocument(html);
    }

    /// <summary>The trimmed text of the element matching <paramref name="selector"/>.</summary>
    private static string TextOf(IHtmlDocument page, string selector)
    {
        IElement? element = page.QuerySelector(selector);
        Assert.NotNull(element);
        return element.TextContent.Trim();
    }

    /// <summary>The text of the element matching <paramref name="selector"/> with every run of white space collapsed to one space.</summary>
    private static string NormalisedTextOf(IHtmlDocument page, string selector)
    {
        return string.Join(' ', TextOf(page, selector).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Polls <paramref name="condition"/> until it is true or ten seconds pass, for a watcher-driven catalog without a bare delay.</summary>
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the host to reflect the change on disk.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
