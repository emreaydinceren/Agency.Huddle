using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins step 1 of the Build a team page <c>BuildTeam</c>: the route, the one h1, the four-step strip, the team type
/// cards (eight to begin with, every type after "All N team types"), "You have one" for a type a Team already uses,
/// and a chosen type completing step 1 and being kept in the address.
/// </summary>
public sealed class BuildTeamTests
{
    private const string Card = "button.team-type-card";

    /// <summary>The page declares the one route <c>/teams/build</c>.</summary>
    [Fact]
    public void Route_IsTeamsBuild()
    {
        string template = Assert.Single(typeof(BuildTeam)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .Select(route => route.Template));

        Assert.Equal("/teams/build", template);
    }

    /// <summary>The page opens with <c>PageHeader</c>: the only h1 reads "Build a team" at Typo.h5, inside a <c>pa-4</c> container.</summary>
    [Fact]
    public async Task Header_IsBuildATeamAsTheOnlyH1()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement heading = Assert.Single(cut.FindAll("h1"));
        Assert.Equal("Build a team", heading.TextContent.Trim());
        Assert.Contains("mud-typography-h5", heading.ClassList);
        Assert.Contains("pa-4", cut.Find(".build-team.mud-container").ClassList);
    }

    /// <summary>With no choice made, the strip lists all four steps and step 1 is the current one.</summary>
    [Fact]
    public async Task Stepper_NothingChosen_ShowsFourStepsWithStepOneCurrent()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IReadOnlyList<IElement> steps = cut.FindAll("ol.build-stepper > li");
        Assert.Equal(["Team type", "Proposal", "Review", "Approve"], steps.Select(step => step.QuerySelector(".build-step-label")?.TextContent.Trim()));
        Assert.Equal("step", steps[0].GetAttribute("aria-current"));
        Assert.Null(steps[1].GetAttribute("aria-current"));
    }

    /// <summary>Every type is a card in the one grid (the stylesheet lays it out in two rows that scroll right) and there is no "All N team types" button.</summary>
    [Fact]
    public async Task Grid_ShowsEveryTypeAndNoAllButton()
    {
        using Fixture fixture = new(typeCount: 10);
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal(10, cut.FindAll(Card).Count);
        Assert.Empty(cut.FindAll("button.build-team-all"));
        Assert.Single(cut.FindAll("div.team-type-grid"));
    }

    /// <summary>A card names its manager and the roles around it: the manager is not counted among the roles.</summary>
    [Fact]
    public async Task Card_CountsRolesBesidesTheManager()
    {
        using Fixture fixture = new(typeCount: 1);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("Manager + 2 roles", cut.Find(".team-type-roles").TextContent.Trim());
    }

    /// <summary>Pressing a card marks it pressed, completes step 1 with the type's name and puts the choice in the address.</summary>
    [Fact]
    public async Task Pick_MarksCardCompletesStepOneAndKeepsChoiceInTheAddress()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.FindAll(Card)[1].ClickAsync());

        Assert.Equal("true", cut.FindAll(Card)[1].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll(Card)[0].GetAttribute("aria-pressed"));
        IElement first = cut.Find("ol.build-stepper > li");
        Assert.Equal("build-step build-step-done", first.ClassName);
        Assert.Equal("build-step build-step-current", cut.FindAll("ol.build-stepper > li")[1].ClassName);
        Assert.Equal("build-step build-step-todo", cut.FindAll("ol.build-stepper > li")[2].ClassName);
        Assert.Equal("Type 02", first.QuerySelector(".build-step-detail")?.TextContent.Trim());
        Assert.Equal("/teams/build?type=t02", new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).PathAndQuery);
    }

    /// <summary>Pressing the chosen card again clears the choice and the address.</summary>
    [Fact]
    public async Task Pick_SameCardAgain_ClearsTheChoice()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());

        Assert.Equal("false", cut.FindAll(Card)[0].GetAttribute("aria-pressed"));
        Assert.DoesNotContain("type=", ctx.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Equal("Choose one", cut.Find("ol.build-stepper > li .build-step-detail").TextContent.Trim());
    }

    /// <summary>A type in the address is chosen on arrival.</summary>
    [Fact]
    public async Task TypeInTheAddress_IsChosenOnArrival()
    {
        using Fixture fixture = new(typeCount: 10);
        await using MudBunitContext ctx = fixture.NewContext();

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t10");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement pressed = Assert.Single(cut.FindAll(Card + "[aria-pressed=true]"));
        Assert.Equal("Type 10", pressed.QuerySelector(".team-type-name")?.TextContent.Trim());
        Assert.Equal(10, cut.FindAll(Card).Count);
    }

    /// <summary>A type a Team already uses is marked "You have one", and the intro names it.</summary>
    [Fact]
    public async Task OwnedType_ShowsYouHaveOneAndIsNamedInTheIntro()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Catalog.Teams = [new TeamSummary("Core", [], [], true) { TeamType = new VersionedRef("t02", "1.0.0") }];
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IElement owned = Assert.Single(cut.FindAll(".team-type-owned"));
        Assert.Equal("You have one", owned.TextContent.Trim());
        Assert.Equal("Type 02", owned.Closest(Card)?.QuerySelector(".team-type-name")?.TextContent.Trim());
        Assert.Equal("Huddle hires a manager and the roles around it. 3 types — you already run Type 02.", cut.Find(".build-team-intro").TextContent.Trim());
    }

    /// <summary>With no Chief of Staff Persona the header has no subtitle and step 2 says it comes from the Chief of Staff.</summary>
    [Fact]
    public async Task NoChiefOfStaff_HasNoSubtitle()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Empty(cut.FindAll(".page-header-subtitle"));
        Assert.Equal("by your Chief of Staff", cut.FindAll("ol.build-stepper > li")[1].QuerySelector(".build-step-detail")?.TextContent.Trim());
    }

    /// <summary>A renamed Chief of Staff is named in the subtitle and in step 2, whatever Name it was given.</summary>
    [Fact]
    public async Task RenamedChiefOfStaff_IsNamedInTheSubtitleAndStepTwo()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Personas.Add(new PersonaIdentity("Margot", "Assistant", "mar", Teams: [], Builtin: "chief-of-staff"), "You are Margot.");
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("with Margot, your Chief of Staff", cut.Find(".page-header-subtitle").TextContent.Trim());
        Assert.Equal("by Margot", cut.FindAll("ol.build-stepper > li")[1].QuerySelector(".build-step-detail")?.TextContent.Trim());
    }

    /// <summary>A Chief of Staff still on its default Name is not named twice: "with your Chief of Staff".</summary>
    [Fact]
    public async Task DefaultNamedChiefOfStaff_ReadsWithYourChiefOfStaff()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Personas.Add(new PersonaIdentity("Chief of Staff", "Assistant", "cos", Teams: [], Builtin: "chief-of-staff"), "You are the Chief of Staff.");
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("with your Chief of Staff", cut.Find(".page-header-subtitle").TextContent.Trim());
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<BuildTeam>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>Real stores behind the page: <c>typeCount</c> team types named "Type 01", "Type 02", … with a manager and two other roles each.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();

        public Fixture(int typeCount)
        {
            this.Personas = TestTaskStore.CreatePersonaStore(this.dir);
            this.Catalog = new FakeTeamCatalog();
            Dictionary<string, string> builtins = new(StringComparer.Ordinal);
            for (int i = 1; i <= typeCount; i++)
            {
                string id = $"t{i:D2}";
                builtins[$"{id}/team-type.json"] = Manifest(id, $"Type {i:D2}", i);
                builtins[$"{id}/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.";
                builtins[$"{id}/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.";
                builtins[$"{id}/candidates/qa.md"] = "---\ntitle: Tester\nconsult_when: checking\n---\nYou test.";
                builtins[$"{id}/processes/flow.md"] = "# Flow";
            }

            this.Types = new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        }

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Personas);
            return ctx;
        }

        public void Dispose()
        {
            this.Personas.Dispose();
            this.dir.Dispose();
        }

        private static string Manifest(string id, string name, int order) => $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "1.0.0", "displayName": "{{name}}", "order": {{order}},
              "workspace": { "kind": "none", "label": "None", "required": false },
              "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
              "roles": [
                { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
                { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md" },
                { "id": "qa", "title": "Tester", "candidate": "candidates/qa.md" }
              ] }
            """;
    }
}
