using Agency.Huddle.Tests.Factories;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teammates;
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

    /// <summary>The page declares the one route <c>/teams/build</c>; an existing Team is edited in its Team page's Details and Members tabs, not on a page of its own.</summary>
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

        _ = cut.InvokeAsync(() => { _ = cut.Find("[aria-label='Browse for library folder']").ClickAsync(); });

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
        Assert.Equal("2 roles available", cut.Find(".build-team-roles-subtitle").TextContent.Trim());
        Assert.NotNull(cut.Find("[aria-label='Add a custom role']"));
        Assert.Equal(["Bookkeeper", "Tutor"], cut.FindAll(".build-team-hire .role-card .team-type-name").Select(name => name.TextContent.Trim()));
    }

    /// <summary>A pool role the chosen type's template defines carries a "Suggested" chip and comes before the others (the Tutor would follow the Bookkeeper by title); the others, and every role under the Blank Team, do not.</summary>
    [Fact]
    public async Task Hire_RoleInTheTemplate_IsMarkedSuggested()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t01");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        IReadOnlyList<IElement> chips = cut.FindAll(".build-team-hire .role-suggested");

        Assert.Equal("Suggested", Assert.Single(chips).TextContent.Trim());
        Assert.Equal(["Add", "Suggested"], chips[0].ParentElement?.Children.Select(child => child.TextContent.Trim()));
        Assert.Equal("Tutor", chips[0].Closest(".role-card")?.QuerySelector(".team-type-name")?.TextContent.Trim());
        Assert.Equal(["Tutor", "Bookkeeper"], cut.FindAll(".build-team-hire .role-card .team-type-name").Select(name => name.TextContent.Trim()));

        await cut.InvokeAsync(() => cut.Find(Card).ClickAsync());
        await cut.InvokeAsync(() => cut.Find(Card).ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll(Card)[0].ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        Assert.Empty(cut.FindAll(".build-team-hire .role-suggested"));
    }

    /// <summary>Add puts a draft Teammate for the role on the roster with a Name, the step strip counts it, and the role can be added again.</summary>
    [Fact]
    public async Task Hire_AddingARole_PutsADraftTeammateOnTheRoster()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        Assert.Equal("No Teammates yet. Add a role above.", cut.Find(".build-team-roster-summary").TextContent.Trim());
        Assert.Equal("Draft Teammates appear here, each with a name and an avatar.", cut.Find(".roster-empty").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        IElement card = Assert.Single(cut.FindAll(".roster-card"));
        Assert.Equal("Bookkeeper", card.QuerySelector(".roster-title")?.TextContent.Trim());
        Assert.NotEqual(string.Empty, card.QuerySelector(".roster-name")?.TextContent.Trim());
        Assert.Equal("balanced", card.QuerySelector(".roster-tier")?.TextContent.Trim());
        Assert.Equal("1 Teammate drafted", cut.FindAll("ol.build-stepper > li")[2].QuerySelector(".build-step-detail")?.TextContent.Trim());
        Assert.Equal("1 draft Teammate · dashed ring = not hired yet", cut.Find(".build-team-roster-summary").TextContent.Trim());
        Assert.Empty(cut.FindAll(".roster-empty"));
        Assert.Equal("On the draft roster", Assert.Single(cut.FindAll(".role-card-taken")).TextContent.Trim());
        Assert.Equal("Bookkeeper", cut.Find(".role-card-taken").Closest(".role-card")?.QuerySelector(".team-type-name")?.TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        Assert.Equal(2, cut.FindAll(".roster-card").Count);
        Assert.DoesNotContain(fixture.Personas.Entries, entry => string.Equals(entry.Title, "Bookkeeper", StringComparison.Ordinal));
    }

    /// <summary>A role with suggested avatars draws one of them; a role without draws from the whole library; the draft shows the image, and Re-roll draws again.</summary>
    [Fact]
    public async Task Hire_AddingARole_DrawsALibraryAvatar()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());

        Assert.Equal("book-1.png", fixture.Library.Reads[0]);
        Assert.Matches(@"^lib-[123]\.png$", fixture.Library.Reads[1]);
        IReadOnlyList<IElement> images = cut.FindAll(".roster-avatar img");
        Assert.Equal(2, images.Count);
        Assert.Equal("data:image/png;base64,AQID", images[0].GetAttribute("src"));

        await cut.InvokeAsync(() => cut.FindAll(".roster-reroll")[1].ClickAsync());

        Assert.Equal(3, fixture.Library.Reads.Count);
    }

    /// <summary>Approve &amp; hire stays off until the details are complete and the roster has a Teammate; then it creates the Team, hires the roster into it and opens it.</summary>
    [Fact]
    public async Task Approve_NeedsDetailsAndATeammate_ThenCreatesTheTeamAndHires()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t01");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        const string Button = "button.build-team-approve-button";
        Assert.True(cut.Find(Button).HasAttribute("disabled"));
        Assert.Equal("Add at least one Teammate to the roster.", cut.Find(".build-team-approve-hint").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync(string.Empty));

        Assert.True(cut.Find(Button).HasAttribute("disabled"));
        Assert.Equal("Finish the team details first.", cut.Find(".build-team-approve-hint").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());

        Assert.False(cut.Find(Button).HasAttribute("disabled"));
        Assert.Equal("Approve & hire", cut.Find(Button).TextContent.Trim());
        Assert.Equal("1 draft Teammate · dashed ring = not hired yet", cut.Find(".build-team-roster-summary").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(Button).ClickAsync());

        Assert.Contains("CreateTeam:Garden", fixture.Folders.Calls);
        Assert.Contains("UpdateTeamFile:Garden", fixture.Folders.Calls);
        PersonaEntry hired = Assert.Single(fixture.Personas.Entries, entry => entry.Teams.Contains("Garden", StringComparer.Ordinal));
        Assert.Equal("Tutor", hired.Title);
        Assert.Equal("/teams/Garden", new Uri(ctx.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath);
    }

    /// <summary>A role's Skills show on its draft, are copied into Skills/ before anything is written, and are carried by the hired Teammate.</summary>
    [Fact]
    public async Task Approve_CopiesTheRolesSkills_AndTheTeammateCarriesThem()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        Assert.Equal("1 skill", cut.Find(".roster-skills").TextContent.Trim());
        Assert.Empty(cut.FindAll(".roster-setup"));

        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        Assert.Equal(["ext-skill"], Assert.Single(fixture.Installer.Calls));
        PersonaEntry hired = Assert.Single(fixture.Personas.Entries, entry => entry.Teams.Contains("Garden", StringComparer.Ordinal));
        Assert.True(PersonaFrontmatter.TryReadIdentity(hired.Text, out PersonaIdentity? identity, out _));
        Assert.Equal(["ext-skill"], identity.Skills);
        Assert.Equal("claude-agent-acp", identity.Adapter);
    }

    /// <summary>A Skill that is not ready stops the approval before the Team is created, names the Skill and links to Settings.</summary>
    [Fact]
    public async Task Approve_SkillNotReady_BlocksBeforeAnythingIsWritten()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.Installer.Missing = [FakeSkillCopier.NotDownloaded("ext-skill")];
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        Assert.Equal("Skills not ready: ext-skill. Copy a prompt for your AI tool from the cards that need setup, then try again.", cut.Find(".build-team-approve-error .mud-alert-message").FirstChild?.TextContent.Trim());
        Assert.Equal("/settings/skills?view=skills", cut.Find(".build-team-skills-link").GetAttribute("href"));
        Assert.DoesNotContain("CreateTeam:Garden", fixture.Folders.Calls);
        Assert.DoesNotContain(fixture.Personas.Entries, entry => entry.Teams.Contains("Garden", StringComparer.Ordinal));
    }

    /// <summary>A role that needs a connector that is not connected is flagged Needs setup with the reason, and does not stop the approval.</summary>
    [Fact]
    public async Task Hire_RoleNeedingAnUnconnectedConnector_IsFlaggedButApprovable()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());

        Assert.Equal("Needs setup: connector email:read not connected", cut.Find(".roster-setup").TextContent.Trim());
        Assert.False(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));
    }

    /// <summary>A name an existing Teammate already has is refused before the Team is created, and nothing is written.</summary>
    [Fact]
    public async Task Approve_NameInUse_IsRefusedBeforeAnythingIsWritten()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());
        string drafted = cut.Find(".roster-name").TextContent.Trim();
        fixture.Personas.Add(new PersonaIdentity(drafted, "Other", drafted.ToLowerInvariant(), Teams: []), "You are other.");

        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        Assert.Equal($"The name '{drafted}' is already in use.", cut.Find(".build-team-approve-error").TextContent.Trim());
        Assert.DoesNotContain("CreateTeam:Garden", fixture.Folders.Calls);
    }

    /// <summary>Re-roll gives the draft another Name, and Remove takes it off the roster.</summary>
    [Fact]
    public async Task Hire_RerollAndRemove_ChangeTheDraft()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());
        string? before = cut.Find(".roster-name").TextContent.Trim();
        Assert.NotNull(cut.Find(".roster-avatar").GetAttribute("style"));

        await cut.InvokeAsync(() => cut.Find(".roster-reroll").ClickAsync());

        Assert.NotEqual(before, cut.Find(".roster-name").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".roster-remove").ClickAsync());

        Assert.Empty(cut.FindAll(".roster-card"));
        Assert.Empty(cut.FindAll(".role-card-taken"));
    }

    /// <summary>The template's manager role, once added, is badged Manager on the roster; other roles are not.</summary>
    [Fact]
    public async Task Hire_TheManagerRole_IsBadged()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t01");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        IReadOnlyList<IElement> cards = cut.FindAll(".roster-card");
        Assert.Equal(2, cards.Count);
        Assert.Equal("Manager", Assert.Single(cards[0].QuerySelectorAll(".roster-manager-badge")).TextContent.Trim());
        Assert.Empty(cards[1].QuerySelectorAll(".roster-manager-badge"));
    }

    /// <summary>The Add card opens the role form; Save stores the role in the custom roles file and shows it as an editable role card.</summary>
    [Fact]
    public async Task Hire_AddingACustomRole_StoresItAndShowsIt()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));

        _ = cut.InvokeAsync(() => { _ = cut.Find("[aria-label='Add a custom role']").ClickAsync(); });
        Assert.Equal("Add a role", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.True(cut.Find("button.custom-role-save").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find(".custom-role-title input").InputAsync("Gardener"));
        await cut.InvokeAsync(() => cut.Find("button.custom-role-save").ClickAsync());

        CustomRole stored = Assert.Single(fixture.Roles.Roles);
        Assert.Equal("Gardener", stored.Title);
        Assert.Equal("custom-gardener", stored.Id);
        Assert.True(File.Exists(Path.Combine(fixture.DirPath, "custom-roles.json")));
        Assert.Equal("Gardener", cut.Find(".build-team-hire .role-card .team-type-name").TextContent.Trim());
        Assert.Empty(cut.FindAll(".roster-card"));
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

        _ = cut.InvokeAsync(() => { _ = cut.Find("[aria-label='Edit Gardener']").ClickAsync(); });
        Assert.Equal("Edit role", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Gardener", cut.Find(".custom-role-title input").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find(".custom-role-title input").InputAsync("Head gardener"));
        await cut.InvokeAsync(() => cut.Find("button.custom-role-save").ClickAsync());

        Assert.Equal("Head gardener", fixture.Roles.Find(role.Id)?.Title);

        _ = cut.InvokeAsync(() => { _ = cut.Find("[aria-label='Delete Head gardener']").ClickAsync(); });
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

    /// <summary>The Details tab shows only the Team's own type and its name disabled, with no header, steps, section numbers or Hire section, and keeps Save off until a detail changes.</summary>
    [Fact]
    public async Task Details_ShowsTheTypeAndDetails_WithTheNameFixed_AndNoHire()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderEdit(ctx, "Garden");

        Assert.Empty(cut.FindAll("h1"));
        Assert.Empty(cut.FindAll(".build-stepper"));
        Assert.Empty(cut.FindAll(".build-team-section-number"));
        Assert.Single(cut.FindAll(".team-details"));
        Assert.Equal("Type 01", Assert.Single(cut.FindAll(Card)).QuerySelector(".team-type-name")?.TextContent.Trim());
        Assert.True(cut.Find(".build-team-name input").HasAttribute("disabled"));
        Assert.Equal("Grows things.", cut.Find(".build-team-purpose textarea").TextContent.Trim());
        Assert.Empty(cut.FindAll(".build-team-hire"));
        Assert.Empty(cut.FindAll(".build-team-savebar"));
        Assert.Empty(cut.FindAll("button.build-team-approve-button"));
    }

    /// <summary>The Members tab shows the Hire section alone: no header, steps, type or details; the members on the roster with the manager badged, and Save off until something changes.</summary>
    [Fact]
    public async Task Members_LoadsTheRoster_WithoutTheRestOfThePage()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        Assert.Empty(cut.FindAll("h1"));
        Assert.Empty(cut.FindAll(".build-stepper"));
        Assert.Empty(cut.FindAll(Card));
        Assert.Empty(cut.FindAll(".build-team-details"));
        Assert.NotNull(cut.Find("[aria-label='Add Bookkeeper']"));
        Assert.Equal(["Ada", "Bo"], cut.FindAll(".roster-name").Select(static name => name.TextContent.Trim()));
        Assert.Equal("Manager", Assert.Single(cut.FindAll(".roster-card")[0].QuerySelectorAll(".roster-manager-badge")).TextContent.Trim());
        Assert.Equal(2, cut.FindAll(".roster-card-existing").Count);
        Assert.Empty(cut.FindAll(".roster-reroll"));
        Assert.Empty(cut.FindAll(".build-team-savebar"));
        Assert.Empty(cut.FindAll("button.build-team-approve-button"));
    }

    /// <summary>The save bar is drawn only once something is unsaved, and it goes again after the save.</summary>
    [Fact]
    public async Task Members_SaveBar_ShowsOnlyWithUnsavedChanges()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");
        Assert.Empty(cut.FindAll(".build-team-savebar"));

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        Assert.False(cut.Find(".build-team-savebar button.build-team-approve-button").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());
        Assert.Empty(cut.FindAll(".build-team-savebar"));
    }

    /// <summary>The roster and role headings carry their count as secondary meta text, and the roles wrap in their own grid.</summary>
    [Fact]
    public async Task Members_Headings_CarryTheirCountAsMeta()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        Assert.Equal("Roster", cut.Find(".build-team-roster .build-team-subhead h3").TextContent.Trim());
        Assert.Contains("chip-meta", cut.Find(".build-team-roster-summary").ClassList);
        Assert.Contains("chip-meta", cut.Find(".build-team-roles-subtitle").ClassList);
        Assert.Single(cut.FindAll(".role-grid"));
    }

    /// <summary>A team type's icon tile is the one quiet tile for every type: no per-type hue class.</summary>
    [Fact]
    public async Task TeamTypeCards_ShareOneQuietIconTile()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build");

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.All(cut.FindAll($"{Card} .team-type-icon"), icon => Assert.Equal("team-type-icon", icon.ClassName));
    }

    /// <summary>A new draft saved into an existing Team is the only Teammate hired; the members are left as they were and the tab stays, with nothing pending.</summary>
    [Fact]
    public async Task Members_AddingADraft_HiresOnlyTheNewOne()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        string adaBefore = fixture.Personas.Get("Ada")?.Text ?? string.Empty;
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        PersonaEntry hired = Assert.Single(fixture.Personas.Entries, entry => string.Equals(entry.Title, "Bookkeeper", StringComparison.Ordinal));
        Assert.Equal(["Garden"], hired.Teams);
        Assert.Equal(3, fixture.Personas.Entries.Count);
        Assert.Equal(adaBefore, fixture.Personas.Get("Ada")?.Text);
        Assert.DoesNotContain("CreateTeam:Garden", fixture.Folders.Calls);
        Assert.Empty(cut.FindAll(".build-team-savebar"));
    }

    /// <summary>Remove marks a member, Undo keeps them; saving asks first, then deletes the Teammate (not only their Team label) and takes them off the roster.</summary>
    [Fact]
    public async Task Members_RemovingAMember_DeletesTheTeammateAfterAsking()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await PickRemoveAsync(cut, "Bo", ".menu-item-destructive");
        Assert.Equal("Will be deleted when you save", cut.Find(".roster-card-removed .roster-removing").TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find("[aria-label='Keep Bo']").ClickAsync());
        Assert.Empty(cut.FindAll(".roster-card-removed"));
        await PickRemoveAsync(cut, "Bo", ".menu-item-destructive");

        _ = cut.InvokeAsync(() => { _ = cut.Find("button.build-team-approve-button").ClickAsync(); });
        Assert.Equal("Delete 1 Teammate?", cut.Find(".mud-dialog-title").TextContent.Trim());
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").Single(button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());

        Assert.Null(fixture.Personas.Get("Bo"));
        Assert.NotNull(fixture.Personas.Get("Ada"));
        Assert.Equal(["Ada"], cut.FindAll(".roster-name").Select(static name => name.TextContent.Trim()));
    }

    /// <summary>Removing the manager blocks Save until another member in a manager role is chosen; saving then makes them the manager before the old one is deleted.</summary>
    [Fact]
    public async Task Members_RemovingTheManager_NeedsANewOne_WhoIsMadeManagerFirst()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await PickRemoveAsync(cut, "Ada", ".menu-item-destructive");

        Assert.True(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));
        Assert.Equal("Choose a new manager before removing Ada.",cut.Find(".build-team-approve-hint").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Tutor']").ClickAsync());
        string successor = cut.FindAll(".roster-name")[^1].TextContent.Trim();

        Assert.False(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));

        _ = cut.InvokeAsync(() => { _ = cut.Find("button.build-team-approve-button").ClickAsync(); });
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog button").Single(button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal(successor, fixture.Folders.ReadResult?.Manager);
        Assert.Null(fixture.Personas.Get("Ada"));
        Assert.NotNull(fixture.Personas.Get(successor));
    }

    /// <summary>Marking every member to go keeps Save off and says the roster must keep someone; the summary counts who leaves and who is deleted.</summary>
    [Fact]
    public async Task Members_RemovingEveryone_KeepsSaveOff()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await PickRemoveAsync(cut, "Ada", ".menu-item-destructive");
        await PickRemoveAsync(cut, "Bo", ".roster-leave");

        Assert.True(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));
        Assert.Equal("Keep at least one Teammate on the roster.", cut.Find(".build-team-approve-hint").TextContent.Trim());
        Assert.Equal("2 members · 1 leaving · 1 to delete", cut.Find(".build-team-roster-summary").TextContent.Trim());
    }

    /// <summary>Remove from team takes the member's Team label away on save without asking, and the Teammate stays.</summary>
    [Fact]
    public async Task Members_RemoveFromTeam_KeepsTheTeammate()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await PickRemoveAsync(cut, "Bo", ".roster-leave");
        Assert.Equal("Will leave the Team when you save", cut.Find(".roster-card-removed .roster-removing").TextContent.Trim());
        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        Assert.Empty(cut.FindAll(".mud-dialog"));
        PersonaEntry? bo = fixture.Personas.ResolveByNameOrAlias("Bo");
        Assert.NotNull(bo);
        Assert.Empty(bo.Teams);
    }

    /// <summary>Each member shows its status dot, and a member that can be messaged has a Message button that reports its Name.</summary>
    [Fact]
    public async Task Members_ShowStatus_AndMessage()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        List<string> messaged = [];
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden", messaged);

        Assert.Equal(2, cut.FindAll(".roster-card .presence-anchor > .agent-dot.on-avatar.online").Count);
        Assert.Empty(cut.FindAll("[aria-label='Message Bo']"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Message Ada']").ClickAsync());

        Assert.Equal(["Ada"], messaged);
    }

    /// <summary>On the Details tab a changed "What it's for" is written to the team file as one line, and the tab stays with nothing pending.</summary>
    [Fact]
    public async Task Details_ChangingWhatItsFor_UpdatesTheTeamFile()
    {
        using Fixture fixture = new(typeCount: 3);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderEdit(ctx, "Garden");

        await cut.InvokeAsync(() => cut.Find(".build-team-purpose textarea").InputAsync("Grows\nvegetables."));
        await cut.InvokeAsync(() => cut.Find("button.build-team-approve-button").ClickAsync());

        Assert.Equal("Grows vegetables.", fixture.Folders.ReadResult?.Owns);
        Assert.Equal("Ada", fixture.Folders.ReadResult?.Manager);
        Assert.Empty(cut.FindAll(".build-team-savebar"));
    }

    /// <summary>The Members tab offers a cloud of the catalog's team types between the "Roles" heading (with its count) and the roles, without the Blank Team, which defines no roles to filter by; every role still shows.</summary>
    [Fact]
    public async Task Members_OffersATeamTypeCloudBetweenTheRolesHeadingAndTheRoles()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        IElement filter = cut.Find(".build-team-role-type-filter");
        IElement? heading = filter.PreviousElementSibling;
        Assert.NotNull(heading);
        Assert.Contains("build-team-subhead", heading.ClassList);
        Assert.Equal("Roles", heading.QuerySelector("h3")?.TextContent.Trim());
        IElement? grid = filter.NextElementSibling;
        Assert.NotNull(grid);
        Assert.Contains("role-grid", grid.ClassList);
        Assert.Equal("Team types", filter.QuerySelector(".chip-cloud-title")?.TextContent.Trim());
        Assert.Equal(["Type 01", "Type 02", "Solo", "Books"], filter.QuerySelectorAll(".chip-cloud-chip-text").Select(chip => chip.TextContent.Trim()));
        Assert.Equal(["Tutor", "Bookkeeper"], RoleNames(cut));
    }

    /// <summary>The Build a team page keeps its roles unfiltered: the team type filter is the Members tab's alone.</summary>
    [Fact]
    public async Task Build_DoesNotOfferTheTeamTypeFilter()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=t01");

        IRenderedComponent<ContainerFragment> cut = Render(ctx);

        Assert.NotEmpty(cut.FindAll(".build-team-hire"));
        Assert.Empty(cut.FindAll(".build-team-role-type-filter"));
        Assert.Equal(["Tutor", "Bookkeeper"], RoleNames(cut));
    }

    /// <summary>
    /// Pressing a type in the cloud floats it to the left of the cloud as a "Team type" chip with no caret and no menu, takes it out of the
    /// cloud's own chips, and keeps only the roles that type defines, the Add card included, so a role the Team's own type suggested goes
    /// when another type is chosen.
    /// </summary>
    [Fact]
    public async Task Members_PickingATeamType_FloatsItLeftAndFiltersTheRolesToWhatItDefines()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await cut.InvokeAsync(() => TypeChip(cut, "Books").ClickAsync());

        IElement chip = cut.Find(".build-team-role-type-chip");
        Assert.Equal("Team type filter: Books", chip.GetAttribute("aria-label"));
        Assert.Equal("Team type filter: Books", cut.Find(".build-team-role-type-filter .chip-cloud").FirstElementChild?.GetAttribute("aria-label"));
        Assert.Equal(["Type 01", "Type 02", "Solo"], CloudTypeNames(cut));
        Assert.Empty(chip.QuerySelectorAll(".task-filter-chip-menu, .mud-button-icon-end"));
        Assert.Equal("Books", chip.QuerySelector(".task-filter-chip-value")?.TextContent.Trim());
        Assert.Equal(["Bookkeeper"], RoleNames(cut));
        Assert.Single(cut.FindAll("[aria-label='Add a custom role']"));
        Assert.Empty(cut.FindAll(".build-team-role-type-empty"));
    }

    /// <summary>The chip's clear button removes the filter: the type goes back among the cloud's chips and every role shows again.</summary>
    [Fact]
    public async Task Members_ClearingTheTeamTypeFilter_PutsTheTypeBackAndShowsEveryRoleAgain()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");
        await cut.InvokeAsync(() => TypeChip(cut, "Books").ClickAsync());

        await cut.InvokeAsync(() => cut.Find("[aria-label='Clear Team type filter']").ClickAsync());

        Assert.Empty(cut.FindAll(".build-team-role-type-chip"));
        Assert.Equal(["Type 01", "Type 02", "Solo", "Books"], CloudTypeNames(cut));
        Assert.Equal(["Tutor", "Bookkeeper"], RoleNames(cut));
    }

    /// <summary>Pressing another type in the cloud changes the filter to it instead, and the type that was chosen goes back among the chips.</summary>
    [Fact]
    public async Task Members_PickingAnotherTeamType_ReplacesTheFilter()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");
        await cut.InvokeAsync(() => TypeChip(cut, "Books").ClickAsync());

        await cut.InvokeAsync(() => TypeChip(cut, "Type 01").ClickAsync());

        Assert.Equal("Team type filter: Type 01", cut.Find(".build-team-role-type-chip").GetAttribute("aria-label"));
        Assert.Equal(["Type 02", "Solo", "Books"], CloudTypeNames(cut));
        Assert.Equal(["Tutor"], RoleNames(cut));
    }

    /// <summary>A type none of whose roles the hiring pool has a job for leaves only the Add card, and says so instead of showing an unexplained empty grid.</summary>
    [Fact]
    public async Task Members_ATeamTypeWithNoHireableRoles_SaysSo()
    {
        using Fixture fixture = new(typeCount: 2, filterTypes: true);
        fixture.SeedGarden();
        await using MudBunitContext ctx = fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderMembers(ctx, "Garden");

        await cut.InvokeAsync(() => TypeChip(cut, "Solo").ClickAsync());

        Assert.Empty(RoleNames(cut));
        Assert.Single(cut.FindAll("[aria-label='Add a custom role']"));
        Assert.Equal("Solo has no roles to hire from here.", cut.Find(".build-team-role-type-empty").TextContent.Trim());
    }

    /// <summary>The role cards' titles in the Hire section, in the order they are drawn.</summary>
    private static IReadOnlyList<string> RoleNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".build-team-hire .role-card .team-type-name").Select(name => name.TextContent.Trim())];

    /// <summary>The names of the team types still offered as chips in the Members tab's cloud, in the order drawn: the chosen type is not among them.</summary>
    private static IReadOnlyList<string> CloudTypeNames(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".build-team-role-type-filter .chip-cloud-chip-text").Select(chip => chip.TextContent.Trim())];

    /// <summary>The chip for a team type in the Members tab's cloud.</summary>
    private static IElement TypeChip(IRenderedComponent<ContainerFragment> cut, string name) =>
        cut.FindAll(".build-team-role-type-filter .chip-cloud-chip").Single(chip => string.Equals(chip.TextContent.Trim(), name, StringComparison.Ordinal));

    /// <summary>Editing a Team that does not exist says so instead of drawing the page.</summary>
    [Fact]
    public async Task Details_UnknownTeam_SaysSo()
    {
        using Fixture fixture = new(typeCount: 3);
        await using MudBunitContext ctx = fixture.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderEdit(ctx, "Nowhere");

        Assert.Equal("There is no Team named \"Nowhere\".", cut.Find(".build-team-missing").TextContent.Trim());
        Assert.Empty(cut.FindAll(".build-team-section"));
    }

    /// <summary>Renders the page as the Details tab does: only the Team set.</summary>
    private static IRenderedComponent<ContainerFragment> RenderEdit(MudBunitContext ctx, string team)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<BuildTeam>(0);
            builder.AddAttribute(1, nameof(BuildTeam.Team), team);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders the page as the Members tab does: members only, with every member Online and messageable, and Message presses recorded in <paramref name="messaged"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderMembers(MudBunitContext ctx, string team, List<string>? messaged = null)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<BuildTeam>(0);
            builder.AddAttribute(1, nameof(BuildTeam.Team), team);
            builder.AddAttribute(2, nameof(BuildTeam.MembersOnly), true);
            builder.AddAttribute(3, nameof(BuildTeam.MemberState), (Func<string, PersonaState>)(static _ => PersonaState.Online));
            builder.AddAttribute(4, nameof(BuildTeam.CanMessage), (Func<string, bool>)(static name => !string.Equals(name, "Bo", StringComparison.Ordinal)));
            builder.AddAttribute(5, nameof(BuildTeam.OnMessage), EventCallback.Factory.Create<string>(new object(), name => messaged?.Add(name)));
            builder.CloseComponent();
        });
    }

    /// <summary>A draft whose role's Skill is not downloaded shows Needs setup and Copy prompt, keeps Approve off with the
    /// setup hint, and turns Ready by itself once the registry finds the Skill (Skill Packs spec §6.10.6).</summary>
    [Fact]
    public async Task BuildTeamSetup_NotDownloaded_WaitsThenFlipsWhenFetched()
    {
        using Fixture fixture = new(typeCount: 3);
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = new();
        host.Source();
        string catalog = host.Catalog("extra", ["ext-skill"]);
        host.SkillsMd([], [catalog]);
        await host.ReadAsync();
        fixture.ExtraServices = services =>
        {
            services.AddSingleton(host.Registry);
            services.AddSingleton<ISkillAvailability>(host.Registry);
            services.AddSingleton(new Agency.Huddle.App.SkillSources.SkillPromptBuilder(new Agency.Huddle.Tests.SkillPacks.Prompts.RecordingPromptSource(), new FixedInstall(Path.Combine(host.DataDir, "InstallSkillPack.md"))));
        };
        await using MudBunitContext ctx = fixture.NewContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/teams/build?type=blank");
        IRenderedComponent<ContainerFragment> cut = Render(ctx);
        await cut.InvokeAsync(() => cut.Find(".build-team-name input").InputAsync("Garden"));
        await cut.InvokeAsync(() => cut.Find("[aria-label='Add Bookkeeper']").ClickAsync());

        Assert.NotEmpty(cut.FindAll(".roster-copy-prompt"));
        Assert.True(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));
        Assert.Equal("Some Teammates need setup. Copy their prompt, paste it into your AI tool, and they update here by themselves.", cut.Find(".build-team-approve-hint").TextContent.Trim());

        host.Catalog("extra", ["ext-skill"], name => host.CloneSkill(name));
        await Task.Run(host.ReadAsync, Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".roster-copy-prompt")));
        Assert.False(cut.Find("button.build-team-approve-button").HasAttribute("disabled"));
    }

    /// <summary>An install file at a fixed path.</summary>
    /// <param name="path">Its full path.</param>
    private sealed class FixedInstall(string path) : Agency.Huddle.App.SkillSources.IInstallFileLocation
    {
        /// <inheritdoc/>
        public string InstallFilePath => path;
    }
    /// <summary>Opens a member's Remove menu and picks one of its items.</summary>
    private static async Task PickRemoveAsync(IRenderedComponent<ContainerFragment> cut, string member, string item)
    {
        await cut.InvokeAsync(() => cut.Find($"[aria-label='Actions for {member}']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(item).ClickAsync());
    }

    private static IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<BuildTeam>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>A library of three images whose reads are recorded, so a test can tell which file a draft drew.</summary>
    private sealed class RecordingLibrary : IAvatarLibraryLoader
    {
        public List<string> Reads { get; } = [];

        public Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            string[] files = ["lib-1.png", "lib-2.png", "lib-3.png"];
            AvatarCatalog catalog = new(files.Select(static file => new LibraryAvatar(file, "Chef", "fair", "man", [])));
            return Task.FromResult(new AvatarLibraryLoadResult(catalog, 0, false));
        }

        public Task<AvatarLibraryImage?> ReadImageAsync(string fileName, CancellationToken cancellationToken)
        {
            this.Reads.Add(fileName);
            return Task.FromResult<AvatarLibraryImage?>(new AvatarLibraryImage([1, 2, 3], ".png"));
        }
    }

    /// <summary>
    /// Real stores behind the page: <c>typeCount</c> team types named "Type 01", "Type 02", … with a manager and two other roles each; Type 02 needs a project folder.
    /// With <c>filterTypes</c> two more follow whose roles differ from those and from each other: "Solo" defines only a role the hiring pool has no job for, and "Books" only the pool's Bookkeeper,
    /// and the catalog sits on the hiring pool, so each type also carries the pool jobs it does not list as Pooled roles.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();

        public Fixture(int typeCount, bool filterTypes = false)
        {
            this.Personas = this.dir.CreatePersonaStore();
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

            if (filterTypes)
            {
                builtins["t98/team-type.json"] = Manifest("t98", "Solo", 98, """{ "id": "solo", "title": "Soloist", "candidate": "candidates/solo.md", "manager": true, "required": true }""");
                builtins["t98/candidates/solo.md"] = "---\ntitle: Soloist\nconsult_when: alone\n---\nYou work alone.";
                builtins["t98/processes/flow.md"] = "# Flow";
                builtins["t99/team-type.json"] = Manifest("t99", "Books", 99, """{ "id": "bookkeeper", "title": "Bookkeeper", "candidate": "candidates/books.md", "manager": true, "required": true }""");
                builtins["t99/candidates/books.md"] = "---\ntitle: Bookkeeper\nconsult_when: receipts\n---\nYou keep books.";
                builtins["t99/processes/flow.md"] = "# Flow";
            }

            this.Roles = new CustomRoleStore(this.dir.Options());
            this.Pool = new HiringPoolCatalog(this.dir.Options(), NullLogger<HiringPoolCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "tutor", "title": "Tutor", "description": "Teaches.", "requires": [ "email:read" ] }, { "id": "bookkeeper", "title": "Bookkeeper", "description": "Keeps the books.", "avatars": [ "book-1.png" ], "skillRefs": [ { "name": "ext-skill", "source": "acme/skills", "path": "skills/ext-skill" } ] } ] }""",
                ["personas/tutor.md"] = "---\ntitle: Tutor\nconsult_when: asked\n---\nYou teach.",
                ["personas/bookkeeper.md"] = "---\ntitle: Bookkeeper\nconsult_when: receipts\n---\nYou keep books.",
            });

            // With the pool behind it the catalog pads every type with the pool jobs it does not list, as Pooled roles,
            // which the team type filter must not count as the type's own. Left off otherwise, so the other tests keep the roles they list.
            this.Types = filterTypes
                ? new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, this.Pool)
                : new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            FakeSkillAvailability packs = new();
            this.Avatars = new AvatarStore(this.dir.Options(), NullLogger<AvatarStore>.Instance);
            HireLimitHarness harness = new(this.dir.Options());
            this.Packs = packs;
            this.Skills = new SkillStore(this.dir.Options(), NullLogger<SkillStore>.Instance);
            this.Hire = new HireService(this.Personas, this.Types, packs, this.Avatars, harness.Guard, this.Installer, new FakeStartGate(), NullLogger<HireService>.Instance);
            this.Membership = new TeamMembership(this.Personas, this.Catalog, this.Folders, this.Types);
        }

        public AvatarStore Avatars { get; }

        public TeamMembership Membership { get; }

        /// <summary>Seeds a Team of type Type 01 with a Lead (its manager) and a Developer, as the catalog and the Teammates report it.</summary>
        public void SeedGarden()
        {
            this.Personas.Add(new PersonaIdentity("Ada", "Lead", "ada", Teams: ["Garden"], Role: new RoleRef("t01", "tutor", "1.0.0")), "You lead.");
            this.Personas.Add(new PersonaIdentity("Bo", "Developer", "bo", Teams: ["Garden"], Skills: ["ext-skill"], Role: new RoleRef("t01", "dev", "1.0.0")), "You build things.");
            this.Catalog.Teams = [new TeamSummary("Garden", [], ["Ada", "Bo"], true) { TeamType = new VersionedRef("t01", "1.0.0"), Manager = "Ada", Owns = "Grows things." }];
            this.Folders.UpdateResult = true;
            this.Folders.ReadResult = new TeamFile(new VersionedRef("t01", "1.0.0"), "Ada", null, "Grows things.", "Process.md", string.Empty);
        }

        public string DirPath => this.dir.Path;

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public CustomRoleStore Roles { get; }

        public HiringPoolCatalog Pool { get; }

        public RecordingLibrary Library { get; } = new();

        public FakeTeamFolders Folders { get; } = new();

        public HireService Hire { get; }

        public FakeSkillCopier Installer { get; } = new();

        public FakeSkillAvailability Packs { get; private set; }

        public SkillStore Skills { get; private set; }

        /// <summary>Registers more services after the fixture's own, so a test can put a real registry in place.</summary>
        public Action<IServiceCollection>? ExtraServices { get; set; }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
            ctx.Services.AddSingleton(this.Types);
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton(this.dir.Options());
            ctx.Services.AddSingleton(this.Roles);
            ctx.Services.AddSingleton(this.Pool);
            ctx.Services.AddSingleton<IAvatarLibraryLoader>(this.Library);
            ctx.Services.AddSingleton<ITeamFolders>(this.Folders);
            ctx.Services.AddSingleton(this.Hire);
            ctx.Services.AddSingleton<ISkillAvailability>(this.Packs);
            this.ExtraServices?.Invoke(ctx.Services);
            ctx.Services.AddSingleton(this.Skills);
            ctx.Services.AddSingleton<IHireContext>(new RoleFixtures.Context());
            ctx.Services.AddSingleton<ITeamMembership>(this.Membership);
            ctx.Services.AddSingleton(this.Avatars);
            ctx.Services.AddSingleton<ITeamDirectory>(this.dir.CreateDirectory());
            ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
            ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
            return ctx;
        }

        public void Dispose()
        {
            this.Personas.Dispose();
            this.dir.Dispose();
        }

        private static string Manifest(string id, string name, int order) => Manifest(id, name, order, """
            { "id": "tutor", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md" },
            { "id": "qa", "title": "Tester", "candidate": "candidates/qa.md" }
            """);

        private static string Manifest(string id, string name, int order, string roles) => $$"""
            { "schemaVersion": 1, "id": "{{id}}", "version": "1.0.0", "displayName": "{{name}}", "order": {{order}},
              "workspace": {{(order == 2 ? """{ "kind": "folder", "label": "Project folder", "required": true }""" : """{ "kind": "none", "label": "None", "required": false }""")}},
              "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
              "roles": [
                {{roles}}
              ] }
            """;
    }
}
