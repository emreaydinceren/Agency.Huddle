using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Ui;

public sealed class TeammatesPageTests
{
    [Fact]
    public async Task TeammatesPage_ListsPersonasFromTheLibrary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

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

        // The form itself lives in the card, which opens on click and so is absent from a plain
        // GET. What the page owes a first-time visitor is the way in. TeammateCardTests covers
        // what the card then shows.
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
    /// the page. Before the fix, a fresh Create card has no Persona yet - <c>cardName</c> starts
    /// empty - and <c>Teammates.ResolveStatus</c> asked <see cref="PersonaHealth.Get"/> about that
    /// blank name anyway. <see cref="PersonaHealth.Get"/>'s own
    /// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> guard is correct - it is a public method on
    /// a public type - so it threw during <c>BuildRenderTree</c>, which is exactly what took the
    /// circuit down. The click is dispatched through the real render pipeline (see
    /// <see cref="ClickNewTeammateAsync"/>), not by calling the private handler directly, so this
    /// test fails the same way the reported crash did: with the same
    /// <see cref="ArgumentException"/>, thrown from the same render pass.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_OpeningTheCreateCard_RendersWithoutThrowing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        // A fresh ServiceCollection wired from the factory's own real, fully-composed services - the
        // same pattern TeammatesPage_RepaintsWhenAnAgentGoesOffline uses for HtmlRenderer, which needs
        // its own root provider rather than factory.Services' request-scoped one. NavigationManager is
        // the one addition: Create mode's <form> makes StaticHtmlRenderer resolve it while writing the
        // form's action attribute, and nothing outside a real HTTP request ever initializes the real one.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        services.AddSingleton<NavigationManager>(new TestNavigationManager());

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Teammates>(ParameterView.Empty));

        await ClickNewTeammateAsync(renderer, output, ct);

        var html = await renderer.Dispatcher.InvokeAsync(() => Task.FromResult(output.ToHtmlString()));

        // The Create card itself, fully rendered - proof this got past ResolveStatus rather than
        // the page having quietly swallowed the crash and shown nothing.
        Assert.Contains("Persona body", html, StringComparison.Ordinal);
        Assert.Contains("Add teammate", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A minimal, initialized <see cref="NavigationManager"/> for <see cref="HtmlRenderer"/> tests
    /// that render a <c>&lt;form&gt;</c>. <see cref="Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure.StaticHtmlRenderer"/>
    /// resolves <see cref="NavigationManager"/> from the render's own service provider to compute a
    /// form's implicit relative action URL, and the real one is only ever initialized by a live
    /// circuit's HTTP request - never by an <see cref="HtmlRenderer"/> used standalone, as this suite
    /// does throughout.
    /// </summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            this.Initialize("http://localhost/", "http://localhost/teammates");
        }
    }

    /// <summary>
    /// Pins the other half of rules.md's model-catalog rule (the first half is
    /// <see cref="TeammatesPage_DoesNotProbeForModelsOnAPlainLoad"/>): "The model catalog is read by
    /// spawning an adapter, even when Acp:Enabled is false... What keeps it honest is when it runs -
    /// only when a New/Edit form opens, never on a page load." The probe spends nothing - it never
    /// starts a Turn - so gating it on <c>Team:Acp:Enabled</c> (which exists because ACP spends
    /// money) would leave the picker empty in the default configuration and defeat the feature.
    /// <see cref="TeamWebApplicationFactory"/> pins <c>Team:Acp:Enabled=false</c> for every test in
    /// this file, so this proves the probe fires here regardless.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_OpeningTheCreateCard_ProbesForModelsEvenWithAcpDisabled()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        // HtmlRenderer needs a scoped provider - Microsoft.AspNetCore.Components.ComponentsActivitySource
        // is a scoped service, and factory.Services is the host's root provider.
        using var scope = factory.Services.CreateScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Teammates>(ParameterView.Empty));

        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);

        await ClickNewTeammateAsync(renderer, output, ct);

        Assert.Equal(1, factory.FakeModelCatalog.ProbeCount);
    }

    [Fact]
    public async Task TeammatesPage_RendersATileThatOpensTheCard()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        // Edit, Open and Remove moved into the card, which a GET cannot open. The tile is the
        // affordance that reaches them, so the tile is what this page-level test can assert.
        Assert.Contains("teammate-tile", html, StringComparison.Ordinal);
        Assert.Contains("coo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ShowsWhetherATeammateIsOnline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("agent-dot", html, StringComparison.Ordinal);
        Assert.Contains("Offline", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stronger claim this phase makes: the front-matter Name beats the FILENAME all the way to
    /// the rendered page, not just through <see cref="Agency.Huddle.App.Acp.PersonaStore.ListNames"/>.
    /// The file is deliberately named "zzz.md" - nothing in it resembling "Chief of Staff" except
    /// the quoted <c>Name</c> field itself.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_ShowsANameThatContainsSpaces()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "zzz.md"),
            "---\nName: 'Chief of Staff'\nTitle: Chief of Staff\nAlias: coo\n---\nYou keep the team honest.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Chief of Staff", html, StringComparison.Ordinal);

        // The monogram is built from the words of the Name, so it is also the page's own evidence
        // that the spaces survived the trip from disk.
        Assert.Contains(">CS<", html, StringComparison.Ordinal);
    }

    /// <summary>The test that proves Team sub-folders are cosmetic: a file nested under "Household" whose Teams field says "Business" must be headed "Business", never "Household".</summary>
    [Fact]
    public async Task TeammatesPage_GroupsByTheTeamsField_NotTheFolder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        var nestedDir = Path.Combine(factory.TeamsDirPath, "Household");
        Directory.CreateDirectory(nestedDir);
        await File.WriteAllTextAsync(
            Path.Combine(nestedDir, "jarvis.md"),
            "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business\n---\nYou are Jarvis.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Business", html, StringComparison.Ordinal);

        // The folder name itself must never leak into a heading.
        Assert.DoesNotContain(">Household<", html, StringComparison.Ordinal);
    }

    /// <summary>A Persona in two Teams is not split across them - the same tile renders once under each heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateInTwoTeams_AppearsUnderBothHeadings()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "amy.md"),
            "---\nName: Amy\nTitle: Router\nAlias: amy\nTeams: Business, Household\n---\nbody",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("Household", html, StringComparison.Ordinal);

        // One tile per heading: Amy's Name has to appear exactly twice, once under each.
        Assert.Equal(2, CountOccurrences(html, "Amy"));
    }

    /// <summary>A Persona whose frontmatter has no Teams field at all falls under the synthetic "No team" heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateWithNoTeams_AppearsUnderTheNoTeamHeading()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("No team", html, StringComparison.Ordinal);
    }

    /// <summary>A file missing a required identity field is listed by its path and reason, above the list, and never renders as a tile.</summary>
    [Fact]
    public async Task TeammatesPage_RejectedFile_IsListedByPathAndReason_AndDoesNotAppearAsATile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        var path = Path.Combine(factory.TeamsDirPath, "broken.md");
        await File.WriteAllTextAsync(path, "---\nName: coo\nTitle: Chief of Staff\n---\nYou are the Chief of Staff.", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains(path, html, StringComparison.Ordinal);
        Assert.Contains("Alias", html, StringComparison.Ordinal);
        Assert.DoesNotContain("teammate-tile", html, StringComparison.Ordinal);
    }

    /// <summary>The filter's own options come off <see cref="Agency.Huddle.App.Acp.PersonaStore.Teams"/>: "All teams" plus every distinct Team name, regardless of how many Personas load.</summary>
    [Fact]
    public async Task TeammatesPage_OffersATeamFilter_ListingAllTeams()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "coo.md"),
            "---\nName: coo\nTitle: Chief of Staff\nAlias: coo\nTeams: Business, Household\n---\nbody",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("All teams", html, StringComparison.Ordinal);
        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("Household", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the T7.1 subscription actually repaints, rather than the page merely reading live
    /// state on every fresh request. An HTTP GET is a brand-new prerender every time and could never
    /// tell those two apart, so this is the one test in this file that renders <see cref="Teammates"/>
    /// directly with <c>HtmlRenderer</c> and re-reads its HTML after an event, the same technique
    /// <c>TeammateCardTests</c> uses to render a component in isolation.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_RepaintsWhenAnAgentGoesOffline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;
        using var dataDir = new TempDataDir();
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Teams"));
        await File.WriteAllTextAsync(Path.Combine(dataDir.Path, "Teams", "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        var directory = new SqliteTeamDirectory(dataDir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);

        var gateway = new FakeAgentGateway();
        gateway.SetOnline(agent.Id);

        var health = new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITeamDirectory>(directory);
        services.AddSingleton<IAgentGateway>(gateway);
        services.AddSingleton(health);
        services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));
        services.AddSingleton<IModelCatalog>(new FakeModelCatalog());

        using var personas = new PersonaStore(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        services.AddSingleton(personas);

        // Never started - Teammates.razor only needs a PersonaSupervisor it can inject, for the T7.2
        // Restart button this test does not exercise.
        using var supervisor = new PersonaSupervisor(
            dataDir.Options(), personas, new FakeAgentHostFactory(), health, new FakeHookSource(), NullLoggerFactory.Instance, NullLogger<PersonaSupervisor>.Instance);
        services.AddSingleton(supervisor);

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Teammates>(ParameterView.Empty));
        var before = await renderer.Dispatcher.InvokeAsync(() => Task.FromResult(output.ToHtmlString()));
        Assert.Contains("Online", before, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", before, StringComparison.Ordinal);

        gateway.SetOffline(agent.Id);

        // The repaint happens off OnHealthOrPresenceChanged's fire-and-forget dispatch, not
        // synchronously with the call above, so this polls rather than reading the HTML once more.
        string after;
        while (true)
        {
            after = await renderer.Dispatcher.InvokeAsync(() => Task.FromResult(output.ToHtmlString()));
            if (after.Contains("Offline", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(20, ct);
        }

        Assert.Contains("Offline", after, StringComparison.Ordinal);
    }

    /// <summary>
    /// Clicks "New teammate" by dispatching the click <c>@onclick="this.BeginCreate"</c> compiles to,
    /// the same way a live circuit drives a real click - rather than invoking
    /// <c>Teammates.BeginCreate</c> directly. That distinction matters here: a throw from
    /// <c>BuildRenderTree</c> during the follow-up render must surface exactly as it would in
    /// production, which calling the private handler and skipping the render would not prove. This
    /// suite has neither bUnit nor a live circuit to click through, and <see cref="HtmlRenderer"/>
    /// does not expose event dispatch on its public surface - only the internal <c>Renderer</c> it
    /// wraps does, in the <c>Microsoft.AspNetCore.Components.RenderTree</c> namespace the framework
    /// itself warns (<c>BL0006</c>) is "not recommended for use outside of the Blazor framework" and
    /// may change shape release to release. Every one of its types is therefore reached here only as
    /// an untyped <see cref="object"/>, whose <see cref="object.GetType()"/> is asked for members by
    /// name at run time rather than named through a compile-time <c>typeof</c>, so this file never
    /// spells a symbol from that namespace and carries no suppression for it - the trade is a helper
    /// that reads by member name rather than by type, and would need updating if a future SDK renamed
    /// one of those members.
    /// </summary>
    /// <param name="renderer">The renderer <paramref name="output"/> was produced from.</param>
    /// <param name="output">The already-rendered <see cref="Teammates"/> root component to click within.</param>
    /// <param name="ct">Cancellation for the dispatch.</param>
    private static async Task ClickNewTeammateAsync(HtmlRenderer renderer, HtmlRootComponent output, CancellationToken ct)
    {
        var componentId = typeof(HtmlRootComponent)
            .GetField("_componentId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(output)!;
        var internalRenderer = typeof(HtmlRootComponent)
            .GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(output)!;

        var rendererType = internalRenderer.GetType().BaseType!;
        var getCurrentRenderTreeFrames = rendererType.GetMethod("GetCurrentRenderTreeFrames", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dispatchEventAsync = rendererType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => string.Equals(m.Name, "DispatchEventAsync", StringComparison.Ordinal) && m.GetParameters().Length == 3);

        var handlerId = await renderer.Dispatcher.InvokeAsync(() =>
        {
            var framesRange = getCurrentRenderTreeFrames.Invoke(internalRenderer, [componentId])!;
            var frames = (Array)framesRange.GetType().GetField("Array")!.GetValue(framesRange)!;
            var frameCount = (int)framesRange.GetType().GetField("Count")!.GetValue(framesRange)!;

            for (var i = 0; i < frameCount; i++)
            {
                var frame = frames.GetValue(i)!;
                var frameType = frame.GetType();
                var kind = frameType.GetProperty("FrameType")!.GetValue(frame)!.ToString();
                if (!string.Equals(kind, "Attribute", StringComparison.Ordinal))
                {
                    continue;
                }

                var attributeName = frameType.GetProperty("AttributeName")!.GetValue(frame) as string;
                var eventHandlerId = (ulong)frameType.GetProperty("AttributeEventHandlerId")!.GetValue(frame)!;
                if (string.Equals(attributeName, "onclick", StringComparison.Ordinal) && eventHandlerId != 0)
                {
                    return eventHandlerId;
                }
            }

            throw new InvalidOperationException("Teammates did not render an 'onclick' handler for 'New teammate'.");
        });

        ct.ThrowIfCancellationRequested();
        await renderer.Dispatcher.InvokeAsync(() => (Task)dispatchEventAsync.Invoke(internalRenderer, [handlerId, null, new MouseEventArgs()])!);
    }

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