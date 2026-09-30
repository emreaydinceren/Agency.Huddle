using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;
using TeammatesPage = Agency.Huddle.App.Components.Pages.Teammates;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Split two ways, per <c>docs/agencyteam/testing.md</c>: routing, HTTP status and
/// <c>ProbeCount</c>/<c>EffortProbeCount</c> are facts about the server, so they stay on a plain HTTP
/// GET against <see cref="TeamWebApplicationFactory"/>. Everything that depends on the exact markup a
/// MudBlazor control renders - the Create card's own content, or a <c>MudSelect</c>'s options, which
/// only exist once opened - now renders <see cref="TeammatesPage"/> through <see cref="MudBunitContext"/>
/// instead, the same split <c>SettingsPageTests</c> uses.
/// </summary>
public sealed class TeammatesPageTests
{
    [Fact]
    public async Task TeammatesPage_ListsPersonasFromTheLibrary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        await factory.WriteDefinitionAsync("coo", PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("coo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_OffersAWayToCreateATeammate()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/teammates", ct);

        // The card itself lives behind IDialogService, which opens on click and so is absent from a
        // plain GET. What the page owes a first-time visitor is the way in - a real <button>'s own
        // prerendered accessible name, which is its own visible text now that it leads the page's
        // toolbar as a labelled button. TeammateCardTests covers what the card then shows.
        Assert.Contains("btn-action-tight", html, StringComparison.Ordinal);
        Assert.Contains("New teammate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ExplainsWhatAPersonaIs()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Markdown", html, StringComparison.Ordinal);

        // A Persona is the file AND the model it thinks with — the intro should say so, since the
        // card now lets a user change that model.
        Assert.Contains("model", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_DoesNotProbeForModelsOnAPlainLoad()
    {
        // This is the guard that keeps the whole suite free and fast, and the reason the model
        // catalog probe is safe to leave ungated on Team:Acp:Enabled: nothing about loading the
        // page opens a card, so nothing about loading the page may spawn an adapter process.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/teammates", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);

        // The Adapter cascade added a third select ahead of Model, so this guards the same
        // rule one layer earlier: a plain load must probe zero Adapters, not just zero Models.
        Assert.Empty(factory.FakeModelCatalog.AdaptersProbed);
    }

    [Fact]
    public async Task TeammatesPage_DoesNotProbeForEffortLevelsOnAPlainLoad()
    {
        // The sibling of the model-catalog guard above: the effort probe spawns the same real
        // adapter process, gated by the same "only when a Create/Edit form opens" rule, so a plain
        // GET must not trigger it either.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/teammates", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.EffortProbeCount);
    }

    /// <summary>
    /// Reproduces Gitea issue #10 (which also closes #11): opening the Create card must not crash
    /// the page. Before the fix, a fresh Create card has no Persona yet, and
    /// <c>Teammates.ResolveStatus</c> asked <see cref="PersonaHealth.Get"/> about that blank name
    /// anyway. bUnit dispatches a real click on the rendered "New teammate" button (see
    /// <see cref="NewTeammateSelector"/>), not by calling the private handler directly, so this test
    /// fails the same way the reported crash did if the guard ever regresses.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_OpeningTheCreateCard_RendersWithoutThrowing()
    {
        await using var factory = new TeamWebApplicationFactory();
        Directory.CreateDirectory(factory.TeammatesDirPath);

        await using var ctx = NewContext(factory);
        var cut = RenderPage(ctx);

        cut.Find(NewTeammateSelector).Click();

        // The Create card itself, fully rendered - proof this got past ResolveStatus rather than
        // the page having quietly swallowed the crash and shown nothing.
        Assert.Contains("Persona body", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Add teammate", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pins the other half of rules.md's model-catalog rule (the first half is
    /// <see cref="TeammatesPage_DoesNotProbeForModelsOnAPlainLoad"/>): "The model catalog is read by
    /// spawning an adapter, even when Acp:Enabled is false... What keeps it honest is when it runs -
    /// only when a New/Edit form opens, never on a page load." <see cref="TeamWebApplicationFactory"/>
    /// pins <c>Team:Acp:Enabled=false</c> for every test in this file, so this proves the probe fires
    /// here regardless.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_OpeningTheCreateCard_ProbesForModelsEvenWithAcpDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        Directory.CreateDirectory(factory.TeammatesDirPath);

        await using var ctx = NewContext(factory);
        var cut = RenderPage(ctx);

        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);

        cut.Find(NewTeammateSelector).Click();

        Assert.Equal(1, factory.FakeModelCatalog.ProbeCount);
    }

    [Fact]
    public async Task TeammatesPage_RendersATileThatOpensTheCard()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        await factory.WriteDefinitionAsync("coo", PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        // Edit, Open and Remove live in the card, which a GET cannot open. The tile is the
        // affordance that reaches them, so the tile is what this page-level test can assert - it is
        // still a real <button class="teammate-tile">, unconverted (see the migration brief's
        // accessibility correction for why).
        Assert.Contains("teammate-tile", html, StringComparison.Ordinal);
        Assert.Contains("coo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ShowsWhetherATeammateIsOnline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        await factory.WriteDefinitionAsync("coo", PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        // agent-dot is left exactly as it is by this migration - Stage 5's job.
        Assert.Contains("agent-dot", html, StringComparison.Ordinal);
        Assert.Contains("Offline", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stronger claim this phase makes: the front-matter Name beats the FILENAME all the way to
    /// the rendered page. The file is deliberately named "zzz.md" - nothing in it resembling "Chief
    /// of Staff" except the quoted <c>Name</c> field itself. Renders through bUnit rather than HTTP:
    /// the monogram now lives inside a <c>MudAvatar</c>, whose markup shape a raw substring match on
    /// ">CS&lt;" can no longer be trusted to find.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_ShowsANameThatContainsSpaces()
    {
        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync(
            "zzz",
            "---\nName: 'Chief of Staff'\nTitle: Chief of Staff\nAlias: coo\n---\nYou keep the team honest.",
            Xunit.TestContext.Current.CancellationToken);

        await using var ctx = NewContext(factory);
        var cut = RenderPage(ctx);

        Assert.Contains("Chief of Staff", cut.Markup, StringComparison.Ordinal);

        // The monogram is built from the words of the Name, so it is also the page's own evidence
        // that the spaces survived the trip from disk.
        var avatar = cut.Find(".mud-avatar");
        Assert.Equal("CS", avatar.TextContent.Trim());
    }

    /// <summary>The test that proves Team sub-folders are cosmetic: a file nested under "Household" whose Teams field says "Business" must be headed "Business", never "Household".</summary>
    [Fact]
    public async Task TeammatesPage_GroupsByTheTeamsField_NotTheFolder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        // ADR-0031: the folder holds only its own definition, so this pins the mismatch case
        // (Scan_FolderNameDiffersFromName_LoadsWithWarning) rather than organisational nesting -
        // the folder is still named differently from the teammate ("Household" vs "Jarvis").
        var folder = Path.Combine(factory.TeammatesDirPath, "Household");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(
            Path.Combine(folder, "Household.md"),
            "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business\n---\nYou are Jarvis.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Business", html, StringComparison.Ordinal);

        // The folder name itself must never leak into a heading - the plain <h2> Part A leaves alone.
        Assert.DoesNotContain(">Household<", html, StringComparison.Ordinal);
    }

    /// <summary>A Persona in two Teams is not split across them - the same tile renders once under each heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateInTwoTeams_AppearsUnderBothHeadings()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        await factory.WriteDefinitionAsync(
            "amy",
            "---\nName: Amy\nTitle: Router\nAlias: amy\nTeams: Business, Household\n---\nbody",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("class=\"teammates-group-heading\">Business<", html, StringComparison.Ordinal);
        Assert.Contains("class=\"teammates-group-heading\">Household<", html, StringComparison.Ordinal);

        // One tile per heading: Amy's name element has to appear exactly twice, once under each. Count
        // the element, never the bare name - the page's prerender markers carry random base64, which
        // contains "Amy" by chance in roughly one run in a hundred.
        Assert.Equal(2, CountOccurrences(html, "class=\"teammates-item-name\">Amy<"));
    }

    /// <summary>A Persona whose frontmatter has no Teams field at all falls under the synthetic "No team" heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateWithNoTeams_AppearsUnderTheNoTeamHeading()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        await factory.WriteDefinitionAsync("coo", PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("No team", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A file missing a required identity field is listed by its path and reason, above the list,
    /// and contributes no tile of its own. Since D15, <see cref="Agency.Huddle.App.Teammates.BuiltinTeammateSeeder"/>
    /// writes a real Chief of Staff into every empty library at startup (Spec §6.12), so this
    /// factory's Teams directory is no longer literally empty by the time the page renders - the
    /// broken file, still the only one this test wrote, must still add nothing beyond that one
    /// seeded tile, never two.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_RejectedFile_IsListedByPathAndReason_AndDoesNotAppearAsATile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        var path = Path.Combine(factory.TeammatesDirPath, "broken", "broken.md");
        await factory.WriteDefinitionAsync("broken", "---\nName: coo\nTitle: Chief of Staff\n---\nYou are the Chief of Staff.", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains(path, html, StringComparison.Ordinal);
        Assert.Contains("Alias", html, StringComparison.Ordinal);

        // Exactly the one seeded Chief of Staff tile - the class attribute value itself, not the
        // bare substring "teammate-tile", which also prefixes "teammate-tile-text", "-role" and
        // "-status" on the very same tile and would over-count.
        Assert.Equal(1, CountOccurrences(html, "class=\"teammate-tile\""));
    }

    /// <summary>
    /// The filter's own options come off <see cref="PersonaStore.Teams"/>: "All teams" plus every
    /// distinct Team name. Renders through bUnit: a <c>MudSelect</c> only paints its options into a
    /// popover once opened by a real click, which a plain GET can never trigger.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_OffersATeamFilter_ListingAllTeams()
    {
        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync(
            "coo",
            "---\nName: coo\nTitle: Chief of Staff\nAlias: coo\nTeams: Business, Household\n---\nbody",
            Xunit.TestContext.Current.CancellationToken);

        await using var ctx = NewContext(factory);
        var cut = RenderPage(ctx);

        var filter = cut.FindAll("div.mud-input-control")
            .First(control => control.QuerySelectorAll("label").Any(label => label.TextContent.Contains("Team", StringComparison.Ordinal)));
        await filter.MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.Contains("All teams", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Business", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Household", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the subscription to <see cref="PersonaStore.PersonasChanged"/>/health events actually
    /// repaints, rather than the page merely reading live state on every fresh request - an HTTP GET
    /// is a brand-new prerender every time and could never tell those two apart. Renders through
    /// <see cref="MudBunitContext"/> rather than a bare <c>HtmlRenderer</c>: the page's MudButton,
    /// MudSelect and MudAvatar all need MudBlazor's DI services to construct at all.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_RepaintsWhenAnAgentGoesOffline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;
        using var dataDir = new TempDataDir();
        Agency.Huddle.Tests.Acp.TestPersonaFiles.Write(new TeammatePaths(dataDir.Options()), "coo", PersonaText("coo", "You are the Chief of Staff."));

        var directory = new SqliteTeamDirectory(dataDir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);

        var gateway = new FakeAgentGateway();
        gateway.SetOnline(agent.Id);

        var health = new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance);

        using var personas = new PersonaStore(
            new TeammatePaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);

        // Never started - Teammates.razor only needs a PersonaSupervisor it can inject, for the
        // Restart button this test does not exercise.
        var resolver = new AdapterProfileResolver(new AdapterCatalog(dataDir.Options()));
        using var skillStore = new SkillStore(dataDir.Options(), NullLogger<SkillStore>.Instance);
        using var supervisor = new PersonaSupervisor(
            dataDir.Options(), personas, new FakeAgentHostFactory(), resolver, health, new FakePromptSource(), new RoomFollows(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance, skillStore);
        using var avatars = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamDirectory>(directory);
        ctx.Services.AddSingleton<IAgentGateway>(gateway);
        ctx.Services.AddSingleton(health);
        ctx.Services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        ctx.Services.AddSingleton<IModelCatalog>(new FakeModelCatalog());
        ctx.Services.AddSingleton(personas);
        ctx.Services.AddSingleton(supervisor);
        ctx.Services.AddSingleton(avatars);

        var cut = ctx.Render<TeammatesPage>();
        Assert.Contains("Online", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", cut.Markup, StringComparison.Ordinal);

        gateway.SetOffline(agent.Id);

        // The repaint happens off OnHealthOrPresenceChanged's fire-and-forget dispatch, not
        // synchronously with the call above, so this polls rather than reading the markup once more.
        while (!cut.Markup.Contains("Offline", StringComparison.Ordinal))
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains("Offline", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Registers this factory's real services (<see cref="PersonaStore"/> and friends) into a fresh <see cref="MudBunitContext"/>, the same pattern <see cref="SettingsPageTests"/> uses.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSpend>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AdapterCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<BuiltinTeammateReset>());
        return ctx;
    }

    /// <summary>Renders the real <see cref="TeammatesPage"/> page, with the popover and dialog providers <see cref="MudBunitContext.RenderWithPopovers"/> supplies so an opened card, and any <c>MudSelect</c> inside it, actually render.</summary>
    private static IRenderedComponent<Bunit.Rendering.ContainerFragment> RenderPage(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TeammatesPage>(0);
            builder.CloseComponent();
        });

    /// <summary>
    /// The CSS selector for the "New teammate" button that <c>OnClick="this.BeginCreate"</c>
    /// compiles to, shared by the two bUnit tests that click it through the real render pipeline
    /// rather than invoking <c>Teammates.BeginCreate</c> directly.
    /// </summary>
    private const string NewTeammateSelector = "button.btn-action-tight";

    /// <summary>Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public async Task Sidebar_LinksToTeammatesPage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("href=\"/teammates\"", html, StringComparison.Ordinal);
    }

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/> - identity is front-matter driven from this phase on, so every seeded Persona needs one to be listed at all.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";
}
