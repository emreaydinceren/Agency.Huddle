using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.HiringPool;
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
    private const string Card = ".build-team-section:not(.build-team-hire) button.team-type-card";

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
        Assert.Equal(["Team type", "Team details", "Hire", "Approve"], steps.Select(step => step.QuerySelector(".build-step-label")?.TextContent.Trim()));
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

        Assert.Equal(11, cut.FindAll(Card).Count);
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

        Assert.Equal("Manager + 2 roles", cut.FindAll(".team-type-roles")[1].TextContent.Trim());
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
        Assert.Equal("false", cut.FindAll(Card)[2].GetAttribute("aria-pressed"));
        IReadOnlyList<IElement> steps = cut.FindAll("ol.build-stepper > li");
        Assert.Equal("build-step build-step-done", steps[0].ClassName);
        Assert.Equal("Type 01", steps[0].QuerySelector(".build-step-detail")?.TextContent.Trim());
        Assert.Equal("/teams/build?type=t01", new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).PathAndQuery);
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
        Assert.Equal(11, cut.FindAll(Card).Count);
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

    /// <summary>With no Chief of Staff Persona the header has no subtitle.</summary>
    [Fact]
    public async Task NoChiefOfStaff_HasNoSubtitle()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Empty(cut.FindAll(".page-header-subtitle"));
    }

    /// <summary>Nothing of step 2 shows until a type is chosen; choosing one opens "Team details" with the type's name filled in.</summary>
    [Fact]
    public async Task Details_ShowOnlyOnceATypeIsChosen_WithTheTypeNameFilledIn()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        Assert.Empty(cut.FindAll(".build-team-details"));

        await cut.InvokeAsync(() => cut.FindAll(Card)[1].ClickAsync());

        Assert.Equal("Team details", cut.Find("#build-team-details-heading").TextContent.Trim());
        Assert.Equal("Type 01", cut.Find(".build-team-name input").GetAttribute("value"));
        Assert.Equal("Team name", cut.Find(".build-team-name label").TextContent.Trim());
        Assert.Equal("What it’s for", cut.Find(".build-team-purpose label").TextContent.Trim());
        Assert.Equal("6", cut.Find(".build-team-purpose textarea").GetAttribute("rows"));
        Assert.Equal("Library folder", cut.Find(".build-team-workspace label").TextContent.Trim());
        IReadOnlyList<IElement> steps = cut.FindAll("ol.build-stepper > li");
        Assert.Equal("build-step build-step-done", steps[1].ClassName);
        Assert.Equal("Type 01", steps[1].QuerySelector(".build-step-detail")?.TextContent.Trim());
        Assert.Equal("build-step build-step-current", steps[2].ClassName);
    }

    /// <summary>A name that matches an existing Team is refused with the same words as New team from a type, and step 2 stays open.</summary>
    [Fact]
    public async Task Details_ExistingTeamName_IsRefusedAndKeepsStepTwoOpen()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Catalog.Teams = [new TeamSummary("Core", [], [], true)];
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.FindAll(Card)[1].ClickAsync());

        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("core"));

        Assert.Equal("A Team with that name exists.", cut.Find(".build-team-name .mud-input-helper-text").TextContent.Trim());
        Assert.Equal("build-step build-step-current", cut.FindAll("ol.build-stepper > li")[1].ClassName);
    }

    /// <summary>Every type shows the same "Library folder" field, filled with the Teams folder under the data folder; a missing folder is refused, a blank one is allowed, and an existing one is accepted.</summary>
    [Fact]
    public async Task Details_LibraryFolder_IsUniformDefaultedAndValidated()
    {
        using Fixture fixture = new(typeCount: 3);
        string teamsFolder = Path.GetFullPath(Path.Combine(fixture.DirPath, new TeamOptions().Teams.Dir));
        Directory.CreateDirectory(teamsFolder);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.FindAll(Card)[2].ClickAsync());

        Assert.Equal("Library folder", cut.Find(".build-team-workspace label").TextContent.Trim());
        Assert.Equal(teamsFolder, cut.Find(".build-team-workspace input").GetAttribute("value"));
        Assert.Equal("build-step build-step-done", cut.FindAll("ol.build-stepper > li")[1].ClassName);

        await cut.InvokeAsync(() => cut.Find(".build-team-workspace input").InputAsync(Path.Combine(fixture.DirPath, "nowhere")));

        Assert.Equal("That folder does not exist.", cut.Find(".build-team-workspace .mud-input-helper-text").TextContent.Trim());
        Assert.Equal("build-step build-step-current", cut.FindAll("ol.build-stepper > li")[1].ClassName);

        await cut.InvokeAsync(() => cut.Find(".build-team-workspace input").InputAsync(string.Empty));

        Assert.Empty(cut.FindAll(".build-team-workspace .mud-input-helper-text"));
        Assert.Equal("build-step build-step-done", cut.FindAll("ol.build-stepper > li")[1].ClassName);
    }

    /// <summary>The hard-coded Blank Team is the first card, says you decide everything, and is not counted among the types.</summary>
    [Fact]
    public async Task BlankTeam_IsTheFirstCard_AndNotCountedAmongTheTypes()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IReadOnlyList<IElement> cards = cut.FindAll(Card);
        Assert.Equal(4, cards.Count);
        Assert.Equal("Blank Team", cards[0].QuerySelector(".team-type-name")?.TextContent.Trim());
        Assert.Equal("You decide everything", cards[0].QuerySelector(".team-type-roles")?.TextContent.Trim());
        Assert.Equal("Start from nothing", cards[0].QuerySelector(".team-type-tagline")?.TextContent.Trim().Split('.')[0]);
        Assert.Equal("Huddle hires a manager and the roles around it. 3 types.", cut.Find(".build-team-intro").TextContent.Trim());
    }

    /// <summary>Choosing the Blank Team leaves the name and the purpose empty for the Human to write, offers the folder as optional, and keeps step 2 open until a name is typed.</summary>
    [Fact]
    public async Task BlankTeam_StartsEmpty_WithAnOptionalFolder_AndNeedsAName()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());

        Assert.Equal("Blank Team", cut.FindAll("ol.build-stepper > li")[0].QuerySelector(".build-step-detail")?.TextContent.Trim());
        Assert.Equal(string.Empty, cut.Find(".build-team-name input").GetAttribute("value") ?? string.Empty);
        Assert.Equal("Library folder", cut.Find(".build-team-workspace label").TextContent.Trim());
        Assert.Equal("build-step build-step-current", cut.FindAll("ol.build-stepper > li")[1].ClassName);
        Assert.Equal("/teams/build?type=blank", new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).PathAndQuery);

        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        Assert.Equal("build-step build-step-done", cut.FindAll("ol.build-stepper > li")[1].ClassName);
    }

    /// <summary>The folder field's Browse button opens the picker at the typed folder; opening a sub-folder and pressing Select puts it in the field.</summary>
    [Fact]
    public async Task Workspace_Browse_PicksASubFolder()
    {
        using Fixture fixture = new(typeCount: 3);
        string repo = Path.Combine(fixture.DirPath, "repo");
        Directory.CreateDirectory(repo);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".build-team-workspace input").InputAsync(fixture.DirPath));

        _ = cut.InvokeAsync(() => cut.Find("[aria-label='Browse for library folder']").Click());

        Assert.Equal("Choose a folder", cut.Find(".mud-dialog-title").TextContent.Trim());
        IElement item = cut.FindAll(".folder-picker-item").Single(candidate => string.Equals(candidate.TextContent.Trim(), "repo", StringComparison.Ordinal));

        await cut.InvokeAsync(() => item.ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.folder-picker-select").ClickAsync());

        Assert.Equal(repo, cut.Find(".build-team-workspace input").GetAttribute("value"));
    }

    /// <summary>Step 3 (Hire) stays hidden until step 2 is complete, then offers the Add card first, then the roles of the hiring pool.</summary>
    [Fact]
    public async Task Hire_ShowsOnceDetailsAreReady_WithTheAddCardFirst()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());
        Assert.Empty(cut.FindAll(".build-team-hire"));

        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        Assert.Equal("Hire", cut.Find("#build-team-hire-heading").TextContent.Trim());
        Assert.Equal("Available roles", cut.Find(".build-team-roles-subtitle").TextContent.Trim());
        Assert.NotNull(cut.Find("[aria-label='Add a custom role']"));
        Assert.Equal(["Bookkeeper", "Tutor"], cut.FindAll(".build-team-hire .role-card .team-type-name").Select(name => name.TextContent.Trim()));
    }

    /// <summary>A pool role the chosen type's template defines carries a "Suggested" chip; the others, and every role under the Blank Team, do not.</summary>
    [Fact]
    public async Task Hire_RoleInTheTemplate_IsMarkedSuggested()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t01");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IReadOnlyList<IElement> chips = cut.FindAll(".build-team-hire .role-suggested");

        Assert.Equal("Suggested", Assert.Single(chips).TextContent.Trim());
        Assert.Equal("Bookkeeper", chips[0].Closest(".role-card")?.QuerySelector(".team-type-name")?.TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(Card).ClickAsync());
        await cut.InvokeAsync(() => cut.Find(Card).ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        Assert.Empty(cut.FindAll(".build-team-hire .role-suggested"));
    }

    /// <summary>Pressing a role hires it, the step strip counts it, and pressing again takes it back.</summary>
    [Fact]
    public async Task Hire_PressingARole_TogglesItAndCountsIt()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        await cut.InvokeAsync(() => cut.Find(".build-team-hire .role-card button.team-type-card").ClickAsync());

        Assert.Equal("true", cut.Find(".build-team-hire .role-card button.team-type-card").GetAttribute("aria-pressed"));
        Assert.Equal("1 role chosen", cut.FindAll("ol.build-stepper > li")[2].QuerySelector(".build-step-detail")?.TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".build-team-hire .role-card button.team-type-card").ClickAsync());

        Assert.Equal("false", cut.Find(".build-team-hire .role-card button.team-type-card").GetAttribute("aria-pressed"));
    }

    /// <summary>The Add card opens the role form; Save stores the role in the custom roles file and shows it as a hired, editable card.</summary>
    [Fact]
    public async Task Hire_AddingACustomRole_StoresItAndHiresIt()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        _ = cut.InvokeAsync(() => cut.Find("[aria-label='Add a custom role']").Click());
        Assert.Equal("Add a role", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.True(cut.Find("button.custom-role-save").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find(".custom-role-title input").InputAsync("Gardener"));
        await cut.InvokeAsync(() => cut.Find("button.custom-role-save").ClickAsync());

        CustomRole stored = Assert.Single(fixture.Roles.Roles);
        Assert.Equal("Gardener", stored.Title);
        Assert.Equal("custom-gardener", stored.Id);
        Assert.True(File.Exists(Path.Combine(fixture.DirPath, "custom-roles.json")));
        Assert.Equal("Gardener", cut.Find(".build-team-hire .role-card .team-type-name").TextContent.Trim());
        Assert.Equal("true", cut.Find(".build-team-hire .role-card button.team-type-card").GetAttribute("aria-pressed"));
    }

    /// <summary>Edit opens the form with the role's fields and stores the changes; Delete asks first and then removes the role.</summary>
    [Fact]
    public async Task Hire_EditAndDeleteACustomRole_ChangeTheStore()
    {
        using Fixture fixture = new(typeCount: 3);
        CustomRole role = fixture.Roles.Add("Gardener", "Grows things.", "You garden.", ModelTier.Fast);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        _ = cut.InvokeAsync(() => cut.Find("[aria-label='Edit Gardener']").Click());
        Assert.Equal("Edit role", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Gardener", cut.Find(".custom-role-title input").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find(".custom-role-title input").InputAsync("Head gardener"));
        await cut.InvokeAsync(() => cut.Find("button.custom-role-save").ClickAsync());

        Assert.Equal("Head gardener", fixture.Roles.Find(role.Id)?.Title);

        _ = cut.InvokeAsync(() => cut.Find("[aria-label='Delete Head gardener']").Click());
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").Single(button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());

        Assert.Empty(fixture.Roles.Roles);
        Assert.Empty(cut.FindAll(".role-card-edit"));
    }

    /// <summary>A renamed Chief of Staff is named in the subtitle, whatever Name it was given.</summary>
    [Fact]
    public async Task RenamedChiefOfStaff_IsNamedInTheSubtitle()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Personas.Add(new PersonaIdentity("Margot", "Assistant", "mar", Teams: [], Builtin: "chief-of-staff"), "You are Margot.");
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.Equal("with Margot, your Chief of Staff", cut.Find(".page-header-subtitle").TextContent.Trim());
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

    /// <summary>Real stores behind the page: <c>typeCount</c> team types named "Type 01", "Type 02", … with a manager and two other roles each; Type 02 needs a project folder.</summary>
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

            this.Roles = new CustomRoleStore(this.dir.Options());
            this.Pool = new HiringPoolCatalog(this.dir.Options(), NullLogger<HiringPoolCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "tutor", "title": "Tutor", "description": "Teaches." }, { "id": "bookkeeper", "title": "Bookkeeper", "description": "Keeps the books." } ] }""",
                ["personas/tutor.md"] = "---\ntitle: Tutor\nconsult_when: asked\n---\nYou teach.",
                ["personas/bookkeeper.md"] = "---\ntitle: Bookkeeper\nconsult_when: receipts\n---\nYou keep books.",
            });
            this.Types = new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        }

        public string DirPath => this.dir.Path;

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public CustomRoleStore Roles { get; }

        public HiringPoolCatalog Pool { get; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.dir.Options());
            ctx.Services.AddSingleton(this.Roles);
            ctx.Services.AddSingleton(this.Pool);
            return ctx;
        }

        public void Dispose()
        {
            this.Personas.Dispose();
            this.dir.Dispose();
        }

        private static string Manifest(string id, string name, int order) => $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "1.0.0", "displayName": "{{name}}", "order": {{order}},
              "workspace": {{(order == 2 ? """{ "kind": "folder", "label": "Project folder", "required": true }""" : """{ "kind": "none", "label": "None", "required": false }""")}},
              "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
              "roles": [
                { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
                { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md" },
                { "id": "bookkeeper", "title": "Tester", "candidate": "candidates/qa.md" }
              ] }
            """;
    }
}
