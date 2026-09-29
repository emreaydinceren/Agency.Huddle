using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Teams;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Pins Spec §6.7's list half: <c>TeamMembers</c> shows one <c>.team-members-row</c> per member of the
/// Team it is given (a <c>StatusDot</c>, a <c>TeammateAvatar</c>, the Name in <c>.team-members-name</c> and
/// <c>Title · Alias</c> in <c>.team-members-role</c>), sorted by Name, filtered by the <c>Search</c> text
/// over Name, Alias and Title, with a "No members yet." / "No members match ..." hint in
/// <c>.team-members-empty</c>. A row click or the row menu's "Open card" opens the same
/// <see cref="TeammateCard"/> the Teammates page does, and the status dot follows the agent's presence
/// and health. Uses the real singletons of a <see cref="TeamWebApplicationFactory"/> (the card's own
/// services), seeded with Nova (Researcher, Scout), Ada (Analyst, Numbers) and Kim (Editor, Scribe).
/// </summary>
public sealed class TeamMembersTests
{
    private const string Nova = "---\nName: Nova\nTitle: Researcher\nAlias: Scout\n---\nYou research.";
    private const string Ada = "---\nName: Ada\nTitle: Analyst\nAlias: Numbers\n---\nYou analyse.";
    private const string Kim = "---\nName: Kim\nTitle: Editor\nAlias: Scribe\n---\nYou edit.";

    /// <summary>Two more Teammates for the Add member rows, spelled so an ordinal sort and an ignore-case sort disagree ("bea" sorts after "Zoe" ordinally).</summary>
    private const string Bea = "---\nName: bea\nTitle: Tester\nAlias: Probe\n---\nYou test.";
    private const string Zoe = "---\nName: Zoe\nTitle: Designer\nAlias: Pixel\n---\nYou design.";

    /// <summary>The <c>·</c> that joins a Title and an Alias in a row's role line.</summary>
    private const string Dot = "·";

    /// <summary>The toolbar's action button ("Add member").</summary>
    private const string AddMemberButton = "button.team-tab-toolbar-action";

    /// <summary>The title bar of the open dialog.</summary>
    private const string DialogTitle = ".mud-dialog-title";

    /// <summary>The inline confirm that replaces a row while its removal is being confirmed.</summary>
    private const string ConfirmBox = ".team-members-confirm";

    /// <summary>The sentence inside the inline confirm.</summary>
    private const string ConfirmText = ".team-members-confirm .team-members-confirm-text";

    /// <summary>Both buttons of the inline confirm.</summary>
    private const string ConfirmButtons = ".team-members-confirm button";

    /// <summary>The inline confirm's Confirm button.</summary>
    private const string ConfirmButton = ".team-members-confirm button.team-members-confirm-button";

    /// <summary>The inline confirm's Cancel button.</summary>
    private const string CancelButton = ".team-members-confirm button.team-members-cancel-button";

    /// <summary>The progress indicator inside the inline confirm.</summary>
    private const string ConfirmProgress = ".team-members-confirm .mud-progress-circular";

    /// <summary>Members given as Nova, Ada, Kim render as Ada, Kim, Nova, each with a status dot, an avatar and the role line.</summary>
    [Fact]
    public async Task Renders_MembersSortedByName_EachWithDotAvatarNameAndRole()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));

        string[] expectedNames = ["Ada", "Kim", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(expectedNames, Names(cut)));
        string[] expectedRoles = [$"Analyst {Dot} Numbers", $"Editor {Dot} Scribe", $"Researcher {Dot} Scout"];
        Assert.Equal(expectedRoles, Roles(cut));
        Assert.Equal(3, cut.FindComponents<StatusDot>().Count);
        Assert.Equal(3, cut.FindComponents<TeammateAvatar>().Count);
        Assert.All(cut.FindAll(".team-members-row"), row =>
        {
            Assert.NotNull(row.QuerySelector("span.agent-dot"));
            Assert.NotNull(row.QuerySelector(".mud-avatar"));
        });
    }

    /// <summary>The toolbar offers the "Add member" action and a search field with the placeholder "Search members".</summary>
    [Fact]
    public async Task Toolbar_ShowsAddMember_AndTheSearchPlaceholder()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Equal("Add member", cut.Find(".team-tab-toolbar-action").TextContent.Trim());
        Assert.Equal("Search members", cut.Find(".team-tab-toolbar-search input").GetAttribute("placeholder"));
    }

    /// <summary>The Search text reaches the toolbar's field.</summary>
    [Fact]
    public async Task Search_Parameter_IsShownInTheToolbarField()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"), "no");

        cut.WaitForAssertion(() => Assert.Equal("no", cut.Find(".team-tab-toolbar-search input").GetAttribute("value")));
    }

    /// <summary>The toolbar's search text is reported through <c>SearchChanged</c>, so the owner can keep it.</summary>
    [Fact]
    public async Task Toolbar_SearchText_IsRaisedThroughSearchChanged()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));

        MudTextField<string> field = cut.FindComponent<MudTextField<string>>().Instance;
        await cut.InvokeAsync(() => field.ValueChanged.InvokeAsync("sco"));

        string?[] expected = ["sco"];
        Assert.Equal(expected, cut.Instance.Searches.ToArray());
    }

    /// <summary>
    /// The Search text keeps the members whose Name (<c>no</c>), Alias (<c>sco</c>) or Title (<c>edit</c>)
    /// contains it, ignoring case (<c>NO</c>): each row of the data matches exactly one member through
    /// exactly one field, so a wrong field shows up as a wrong list.
    /// </summary>
    [Theory]
    [InlineData("no", "Nova")]
    [InlineData("sco", "Nova")]
    [InlineData("edit", "Kim")]
    [InlineData("NO", "Nova")]
    public async Task Search_KeepsOnlyTheMembersMatchingNameAliasOrTitle_IgnoringCase(string search, string expectedName)
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"), search);

        string[] expected = [expectedName];
        cut.WaitForAssertion(() => Assert.Equal(expected, Names(cut)));
    }

    /// <summary>A Search nobody matches shows the "No members match" hint with the text in quotes, and no rows.</summary>
    [Fact]
    public async Task Search_MatchingNobody_ShowsTheNoMatchHint()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"), "zz");

        cut.WaitForAssertion(() => Assert.Equal("No members match \"zz\".", cut.Find(".team-members-empty").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".team-members-row"));
    }

    /// <summary>A Team with no members shows "No members yet." and no rows, and still offers "Add member" so the first one can be added.</summary>
    [Fact]
    public async Task TeamWithNoMembers_ShowsTheNoMembersYetHint_AndStillOffersAddMember()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business"));

        cut.WaitForAssertion(() => Assert.Equal("No members yet.", cut.Find(".team-members-empty").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".team-members-row"));
        Assert.Equal("Add member", cut.Find(".team-tab-toolbar-action").TextContent.Trim());
    }

    /// <summary>While <c>Team</c> is still <see langword="null"/> nothing renders: no toolbar, no rows, no hint.</summary>
    [Fact]
    public async Task NullTeam_RendersNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, null);

        Assert.Empty(cut.FindAll(".team-tab-toolbar"));
        Assert.Empty(cut.FindAll(".team-members-row"));
        Assert.Empty(cut.FindAll(".team-members-empty"));
    }

    /// <summary>The list follows a new <c>Team</c> parameter: a member added to the Team appears, in sorted position.</summary>
    [Fact]
    public async Task NewTeamParameter_ReRendersTheList()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada"));
        string[] before = ["Ada", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(before, Names(cut)));

        cut.Render(parameters => parameters.Add(host => host.Team, Team("Business", "Nova", "Ada", "Kim")));

        string[] after = ["Ada", "Kim", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(after, Names(cut)));
    }

    /// <summary>A member's Persona is found ignoring case: the display spelling "nova" still shows Nova's Title and Alias.</summary>
    [Fact]
    public async Task MemberSpelledInAnotherCase_StillShowsItsRole()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "nova"));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".team-members-row")));
        Assert.Equal($"Researcher {Dot} Scout", Assert.Single(Roles(cut)));
    }

    /// <summary>A member with no Persona entry (a race with a delete) still gets its row, with an empty role and no throw.</summary>
    [Fact]
    public async Task MemberWithNoPersona_StillGetsARow_WithAnEmptyRole()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Ghost", "Nova"));

        string[] expectedNames = ["Ghost", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(expectedNames, Names(cut)));
        string[] expectedRoles = ["", $"Researcher {Dot} Scout"];
        Assert.Equal(expectedRoles, Roles(cut));
    }

    /// <summary>
    /// A row click opens <see cref="TeammateCard"/> with the Name, Title and Alias of that member (the
    /// full parameter set <c>Teammates.BuildViewParameters</c> builds, not the Name alone).
    /// </summary>
    [Fact]
    public async Task RowClick_OpensTheCard_WithNameTitleAndAlias()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Empty(cut.FindComponents<TeammateCard>());

        AngleSharp.Dom.IElement row = RowFor(cut, "Nova");
        _ = cut.InvokeAsync(() => row.Click());

        TeammateCard card = OpenedCard(cut);
        Assert.Equal("Nova", card.Name);
        Assert.Equal("Researcher", card.Title);
        Assert.Equal("Scout", card.Alias);
        Assert.Equal(TeammateCardMode.View, card.Mode);
    }

    /// <summary>The row menu is a "Member actions" button whose menu holds exactly "Open card" and "Remove from Team".</summary>
    [Fact]
    public async Task RowMenu_HoldsExactlyOpenCardAndRemoveFromTeam()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Empty(MenuItemTexts(cut));

        AngleSharp.Dom.IElement button = MenuButton(cut, "Nova");
        await cut.InvokeAsync(() => button.ClickAsync());

        string[] expected = ["Open card", "Remove from Team"];
        Assert.Equal(expected, MenuItemTexts(cut));
    }

    /// <summary>The menu's "Open card" does what a row click does: the card opens with that member's Name, Title and Alias.</summary>
    [Fact]
    public async Task RowMenu_OpenCard_OpensTheCardOfThatMember()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        await cut.InvokeAsync(() => MenuButton(cut, "Kim").ClickAsync());
        string[] expectedItems = ["Open card", "Remove from Team"];
        Assert.Equal(expectedItems, MenuItemTexts(cut));
        Assert.Empty(cut.FindComponents<TeammateCard>());

        AngleSharp.Dom.IElement item = cut.FindAll("div.mud-menu-item").Single(candidate => string.Equals(candidate.TextContent.Trim(), "Open card", StringComparison.Ordinal));
        _ = cut.InvokeAsync(() => item.Click());

        TeammateCard card = OpenedCard(cut);
        Assert.Equal("Kim", card.Name);
        Assert.Equal("Editor", card.Title);
        Assert.Equal("Scribe", card.Alias);
    }

    /// <summary>Clicking the <c>⋯</c> button opens the menu but not the card: the click does not reach the row.</summary>
    [Fact]
    public async Task ClickingTheMenuButton_OpensTheMenu_AndNoDialog()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Empty(MenuItemTexts(cut));

        AngleSharp.Dom.IElement button = MenuButton(cut, "Nova");
        await cut.InvokeAsync(() => button.ClickAsync());

        string[] expected = ["Open card", "Remove from Team"];
        Assert.Equal(expected, MenuItemTexts(cut));
        Assert.Empty(cut.FindComponents<TeammateCard>());
        Assert.Empty(cut.FindAll(".mud-dialog"));
    }

    /// <summary>A member's dot follows its agent: offline until the agent connects, then online, and only that member's dot changes.</summary>
    [Fact]
    public async Task StatusDot_FollowsThePresenceOfTheMembersAgent()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        User novaAgent = await AgentAsync(ctx, "Nova", ct);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Equal("agent-dot offline", DotClass(cut, "Nova"));

        factory.FakeAgentGateway.SetOnline(novaAgent.Id);

        cut.WaitForAssertion(() => Assert.Equal("agent-dot online", DotClass(cut, "Nova")));
        Assert.Equal("agent-dot offline", DotClass(cut, "Ada"));
    }

    /// <summary>A health report for a connected member repaints its dot: online, then degraded once <c>PersonaHealth</c> says so.</summary>
    [Fact]
    public async Task StatusDot_FollowsAHealthReport()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory);
        User novaAgent = await AgentAsync(ctx, "Nova", ct);
        factory.FakeAgentGateway.SetOnline(novaAgent.Id);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal("agent-dot online", DotClass(cut, "Nova")));

        ctx.Services.GetRequiredService<PersonaHealth>().Report("Nova", PersonaState.Degraded, "slow");

        cut.WaitForAssertion(() => Assert.Equal("agent-dot degraded", DotClass(cut, "Nova")));
    }

    /// <summary>
    /// Clicking "Add member" opens <see cref="AddMemberDialog"/> titled "Add member to Business", small and
    /// full-width, with the Team it was given and, as Candidates, the Teammates that are not members
    /// (compared ignoring case: "nova" and "KIM" exclude Nova and Kim), sorted by Name ignoring case, each
    /// with its Title and Alias.
    /// </summary>
    [Fact]
    public async Task AddMember_Click_OpensTheDialog_WithCandidatesExcludingMembers()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await SeedExtraAsync(factory);
        await using MudBunitContext ctx = NewContext(factory, new FakeTeamMembership());
        string[] directoryNames = [.. ctx.Services.GetRequiredService<PersonaStore>().Entries.Select(static entry => entry.Name).Order(StringComparer.OrdinalIgnoreCase)];
        string[] expectedDirectory = ["Ada", "bea", "Chief of Staff", "Kim", "Nova", "Zoe"];
        Assert.Equal(expectedDirectory, directoryNames);
        TeamSummary team = Team("Business", "nova", "KIM");
        IRenderedComponent<Host> cut = RenderHost(ctx, team);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".team-members-row").Count));
        Assert.Empty(cut.FindComponents<AddMemberDialog>());

        AddMemberDialog dialog = await OpenAddMemberAsync(cut);

        Assert.Equal("Add member to Business", cut.Find(DialogTitle).TextContent.Trim());
        Assert.Single(cut.FindAll(".mud-dialog.mud-dialog-width-sm.mud-dialog-width-full"));
        Assert.Same(team, dialog.Team);
        string[] candidateNames = [.. dialog.Candidates.Select(static choice => choice.Name)];
        string[] expectedNames = ["Ada", "bea", "Chief of Staff", "Zoe"];
        Assert.Equal(expectedNames, candidateNames);
        TeammateChoice[] expectedChoices = [new("Ada", "Analyst", "Numbers"), new("bea", "Tester", "Probe"), new("Chief of Staff", "Chief of Staff", "cos"), new("Zoe", "Designer", "Pixel")];
        Assert.Equal(expectedChoices, dialog.Candidates);
    }

    /// <summary>
    /// Picking Kim and pressing Add in the dialog shows the snackbar "Kim added to Business." (Success). The
    /// dialog adds the member itself, so <c>TeamMembers</c> must not call the membership service again: exactly
    /// one call was made.
    /// </summary>
    [Fact]
    public async Task AddMember_WhenTheDialogReturnsAName_ShowsTheSnackbar()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new();
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".team-members-row").Count));
        Assert.Empty(snackbar.ShownSnackbars);
        _ = await OpenAddMemberAsync(cut);
        await PickAsync(cut, "Kim");

        await cut.InvokeAsync(() => cut.Find(".add-member-dialog-add-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Kim added to Business.", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
        string[] expectedCalls = ["Add:Business/Kim"];
        Assert.Equal(expectedCalls, membership.Calls);
    }

    /// <summary>Cancelling the dialog after a pick shows no snackbar and adds nobody.</summary>
    [Fact]
    public async Task AddMember_WhenCancelled_ShowsNoSnackbar()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new();
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada"));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".team-members-row").Count));
        _ = await OpenAddMemberAsync(cut);
        await PickAsync(cut, "Kim");

        await cut.InvokeAsync(() => cut.Find(".add-member-dialog-cancel-button").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(DialogTitle)));
        Assert.Empty(snackbar.ShownSnackbars);
        Assert.Empty(membership.Calls);
    }

    /// <summary>When every Teammate is already a member the dialog still opens, with an empty Candidates list and nothing to pick.</summary>
    [Fact]
    public async Task AddMember_NoCandidates_StillOpensTheDialog()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        await using MudBunitContext ctx = NewContext(factory, new FakeTeamMembership());
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim", "Chief of Staff"));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(".team-members-row").Count));

        AddMemberDialog dialog = await OpenAddMemberAsync(cut);

        Assert.Equal("Add member to Business", cut.Find(DialogTitle).TextContent.Trim());
        Assert.Empty(dialog.Candidates);
        IEnumerable<TeammateChoice> found = await SearchAsync(cut, null);
        Assert.Empty(found);
    }

    /// <summary>
    /// The menu's "Remove from Team" swaps Ada's row for an inline confirm (no dialog): the sentence, then
    /// <c>Confirm</c> and <c>Cancel</c>; the other rows stay and nothing is called yet.
    /// </summary>
    [Fact]
    public async Task Remove_Click_SwapsTheRowForAnInlineConfirm()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new();
        await using MudBunitContext ctx = NewContext(factory, membership);
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        string[] before = ["Ada", "Kim", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(before, Names(cut)));
        Assert.Empty(cut.FindAll(ConfirmBox));

        await BeginRemovalAsync(cut, "Ada");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));
        Assert.Equal("Remove Ada from Business? Ada restarts and loses its conversation memory.", cut.Find(ConfirmText).TextContent.Trim());
        string[] expectedButtons = ["Confirm", "Cancel"];
        Assert.Equal(expectedButtons, ConfirmButtonTexts(cut));
        string[] remaining = ["Kim", "Nova"];
        Assert.Equal(remaining, Names(cut));
        Assert.Empty(cut.FindAll(DialogTitle));
        Assert.Empty(cut.FindComponents<TeammateCard>());
        Assert.Empty(membership.Calls);
    }

    /// <summary>
    /// Confirm calls <c>Remove("Business", "Ada")</c> once and shows "Ada removed from Business." (Success); the
    /// confirm closes and Ada's row is back, because the row leaves only when the Team parameter changes.
    /// </summary>
    [Fact]
    public async Task Confirm_CallsMembershipRemove_OnceAndShowsTheSnackbar()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new() { MembershipResult = new(MembershipOutcome.Removed) };
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Empty(snackbar.ShownSnackbars);
        await BeginRemovalAsync(cut, "Ada");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));

        await cut.InvokeAsync(() => cut.Find(ConfirmButton).ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Ada removed from Business.", shown.Message);
        Assert.Equal(Severity.Success, shown.Severity);
        string[] expectedCalls = ["Remove:Business/Ada"];
        Assert.Equal(expectedCalls, membership.Calls);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ConfirmBox)));
        string[] rows = ["Ada", "Kim", "Nova"];
        Assert.Equal(rows, Names(cut));
    }

    /// <summary>After a successful removal Ada's row stays until the Team parameter changes; a Team without Ada then drops it.</summary>
    [Fact]
    public async Task Removed_KeepsTheRowUntilTheTeamParameterChanges()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new() { MembershipResult = new(MembershipOutcome.Removed) };
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        await BeginRemovalAsync(cut, "Ada");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));
        await cut.InvokeAsync(() => cut.Find(ConfirmButton).ClickAsync());
        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ConfirmBox)));
        string[] rowsBefore = ["Ada", "Kim", "Nova"];
        Assert.Equal(rowsBefore, Names(cut));

        cut.Render(parameters => parameters.Add(host => host.Team, Team("Business", "Nova", "Kim")));

        string[] rowsAfter = ["Kim", "Nova"];
        cut.WaitForAssertion(() => Assert.Equal(rowsAfter, Names(cut)));
    }

    /// <summary>Cancel closes the confirm, restores Ada's row and calls nothing.</summary>
    [Fact]
    public async Task Cancel_RestoresTheRow_AndCallsNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new() { MembershipResult = new(MembershipOutcome.Removed) };
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        await BeginRemovalAsync(cut, "Ada");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));

        await cut.InvokeAsync(() => cut.Find(CancelButton).ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ConfirmBox)));
        string[] rows = ["Ada", "Kim", "Nova"];
        Assert.Equal(rows, Names(cut));
        Assert.Empty(membership.Calls);
        Assert.Empty(snackbar.ShownSnackbars);
    }

    /// <summary>
    /// While the removal runs the confirm shows a progress indicator and both its buttons are disabled; once the
    /// call returns the indicator is gone. The fake's <c>Hold</c> keeps the call running until the test releases it.
    /// </summary>
    [Fact]
    public async Task Confirm_WhileTheCallRuns_ShowsProgressAndDisablesTheButtons()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        using SemaphoreSlim hold = new(0);
        FakeTeamMembership membership = new() { MembershipResult = new(MembershipOutcome.Removed), Hold = hold };
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        await BeginRemovalAsync(cut, "Ada");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));
        Assert.Empty(cut.FindAll(ConfirmProgress));
        Assert.All(cut.FindAll(ConfirmButtons), button => Assert.False(button.HasAttribute("disabled")));

        _ = cut.InvokeAsync(() => cut.Find(ConfirmButton).Click());

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmProgress)));
        Assert.Equal(2, cut.FindAll(ConfirmButtons).Count);
        Assert.All(cut.FindAll(ConfirmButtons), button => Assert.True(button.HasAttribute("disabled")));
        Assert.Empty(snackbar.ShownSnackbars);

        hold.Release();

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ConfirmBox)));
        Assert.Empty(cut.FindAll(ConfirmProgress));
    }

    /// <summary>
    /// A removal that does not happen reports an error snackbar (Severity.Error) naming why, closes the confirm and
    /// keeps the row: not-a-member and no-such-Persona have their own texts, a rejected write shows the store's
    /// message, or a fallback when it gave none.
    /// </summary>
    [Theory]
    [InlineData(MembershipOutcome.NotMember, null, "Ada is not in Business.")]
    [InlineData(MembershipOutcome.NotFound, null, "Ada no longer exists.")]
    [InlineData(MembershipOutcome.Rejected, "The disk is full.", "The disk is full.")]
    [InlineData(MembershipOutcome.Rejected, null, "Couldn't remove Ada from Business.")]
    public async Task Remove_NotMemberNotFoundOrRejected_ShowsAnErrorSnackbar(MembershipOutcome outcome, string? problem, string expectedMessage)
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedAsync(factory);
        FakeTeamMembership membership = new() { MembershipResult = new(outcome, problem) };
        await using MudBunitContext ctx = NewContext(factory, membership);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<Host> cut = RenderHost(ctx, Team("Business", "Nova", "Ada", "Kim"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".team-members-row").Count));
        Assert.Empty(snackbar.ShownSnackbars);
        await BeginRemovalAsync(cut, "Ada");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(ConfirmBox)));

        await cut.InvokeAsync(() => cut.Find(ConfirmButton).ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal(expectedMessage, shown.Message);
        Assert.Equal(Severity.Error, shown.Severity);
        string[] expectedCalls = ["Remove:Business/Ada"];
        Assert.Equal(expectedCalls, membership.Calls);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(ConfirmBox)));
        string[] rows = ["Ada", "Kim", "Nova"];
        Assert.Equal(rows, Names(cut));
    }

    /// <summary>Opens <paramref name="name"/>'s row menu and clicks its "Remove from Team" item.</summary>
    private static async Task BeginRemovalAsync(IRenderedComponent<Host> cut, string name)
    {
        await cut.InvokeAsync(() => MenuButton(cut, name).ClickAsync());
        AngleSharp.Dom.IElement item = cut.FindAll("div.mud-menu-item").Single(candidate => string.Equals(candidate.TextContent.Trim(), "Remove from Team", StringComparison.Ordinal));
        await cut.InvokeAsync(() => item.ClickAsync());
    }

    /// <summary>The trimmed text of each button in the open inline confirm, in render order.</summary>
    private static string[] ConfirmButtonTexts(IRenderedComponent<Host> cut) =>
        [.. cut.FindAll(ConfirmButtons).Select(button => button.TextContent.Trim())];

    /// <summary>Adds Bea and Zoe as definition files, before the factory's host is built.</summary>
    private static async Task SeedExtraAsync(TeamWebApplicationFactory factory)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await factory.WriteDefinitionAsync("bea", Bea, ct);
        await factory.WriteDefinitionAsync("Zoe", Zoe, ct);
    }

    /// <summary>Clicks the toolbar's "Add member" button and returns the <see cref="AddMemberDialog"/> once it is on screen.</summary>
    private static async Task<AddMemberDialog> OpenAddMemberAsync(IRenderedComponent<Host> cut)
    {
        _ = cut.InvokeAsync(() => cut.Find(AddMemberButton).Click());
        cut.WaitForState(() => cut.FindComponents<AddMemberDialog>().Count == 1);
        return cut.FindComponent<AddMemberDialog>().Instance;
    }

    /// <summary>Awaits the open dialog autocomplete's own <c>SearchFunc</c> for <paramref name="term"/>.</summary>
    private static async Task<IEnumerable<TeammateChoice>> SearchAsync(IRenderedComponent<Host> cut, string? term)
    {
        IRenderedComponent<MudAutocomplete<TeammateChoice>> autocomplete = cut.FindComponent<MudAutocomplete<TeammateChoice>>();
        Func<string?, CancellationToken, Task<IEnumerable<TeammateChoice>>?> search = autocomplete.Instance.SearchFunc
            ?? throw new InvalidOperationException("Autocomplete has no SearchFunc.");
        Task<IEnumerable<TeammateChoice>>? searchTask = search(term, Xunit.TestContext.Current.CancellationToken);
        return searchTask is null ? throw new InvalidOperationException("SearchFunc returned null.") : await searchTask;
    }

    /// <summary>Picks the first suggestion for <paramref name="term"/> in the open dialog's autocomplete, as a click on it would.</summary>
    private static async Task PickAsync(IRenderedComponent<Host> cut, string term)
    {
        IRenderedComponent<MudAutocomplete<TeammateChoice>> autocomplete = cut.FindComponent<MudAutocomplete<TeammateChoice>>();
        TeammateChoice first = (await SearchAsync(cut, term)).First();
        await cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(first));
    }

    /// <summary>Seeds Nova, Ada and Kim as definition files, before the factory's host is built.</summary>
    private static async Task SeedAsync(TeamWebApplicationFactory factory)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await factory.WriteDefinitionAsync("Nova", Nova, ct);
        await factory.WriteDefinitionAsync("Ada", Ada, ct);
        await factory.WriteDefinitionAsync("Kim", Kim, ct);
    }

    /// <summary>Registers this factory's real services (the same set <c>TeammatesPageTests</c> uses, which also satisfies <see cref="TeammateCard"/>) into a fresh <see cref="MudBunitContext"/>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory, FakeTeamMembership? membership = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamMembership>(membership ?? new FakeTeamMembership());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AdapterCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<SkillStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<BuiltinTeammateReset>());
        return ctx;
    }

    /// <summary>The agent user of <paramref name="name"/>, created in the directory the context's components read.</summary>
    private static async Task<User> AgentAsync(MudBunitContext ctx, string name, CancellationToken ct)
    {
        User? agent = await ctx.Services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(name, null, ct);
        Assert.NotNull(agent);
        return agent;
    }

    private static IRenderedComponent<Host> RenderHost(MudBunitContext ctx, TeamSummary? team, string? search = null) =>
        ctx.Render<Host>(parameters => parameters.Add(host => host.Team, team).Add(host => host.Search, search));

    /// <summary>A Team called <paramref name="name"/> whose members are <paramref name="members"/>, in the order given.</summary>
    private static TeamSummary Team(string name, params string[] members) => new(name, [], members, true);

    /// <summary>The trimmed text of every <c>.team-members-name</c>, in render order.</summary>
    private static string[] Names(IRenderedComponent<Host> cut) =>
        [.. cut.FindAll(".team-members-name").Select(element => element.TextContent.Trim())];

    /// <summary>The trimmed text of every <c>.team-members-role</c>, in render order.</summary>
    private static string[] Roles(IRenderedComponent<Host> cut) =>
        [.. cut.FindAll(".team-members-role").Select(element => element.TextContent.Trim())];

    /// <summary>The <c>.team-members-row</c> whose <c>.team-members-name</c> is <paramref name="name"/>.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<Host> cut, string name) =>
        cut.FindAll(".team-members-row").Single(row => string.Equals(row.QuerySelector(".team-members-name")?.TextContent.Trim(), name, StringComparison.Ordinal));

    /// <summary>The "Member actions" button of <paramref name="name"/>'s row.</summary>
    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<Host> cut, string name) =>
        RowFor(cut, name).QuerySelectorAll("button[aria-label='Member actions']").Single();

    /// <summary>The <c>class</c> attribute of the <c>agent-dot</c> in <paramref name="name"/>'s row.</summary>
    private static string? DotClass(IRenderedComponent<Host> cut, string name) =>
        RowFor(cut, name).QuerySelectorAll("span.agent-dot").Single().GetAttribute("class");

    /// <summary>The trimmed text of every open <c>MudMenu</c> item.</summary>
    private static string[] MenuItemTexts(IRenderedComponent<Host> cut) =>
        [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>Waits until exactly one <see cref="TeammateCard"/> is open and returns it.</summary>
    private static TeammateCard OpenedCard(IRenderedComponent<Host> cut)
    {
        cut.WaitForState(() => cut.FindComponents<TeammateCard>().Count == 1);
        return cut.FindComponent<TeammateCard>().Instance;
    }

    /// <summary>
    /// Hosts <c>TeamMembers</c> next to the popover and dialog providers its menu and the opened card need,
    /// with <c>Team</c> and <c>Search</c> as parameters of its own so a test can render again with new
    /// ones, and records every <c>SearchChanged</c> value.
    /// </summary>
    private sealed class Host : ComponentBase
    {
        /// <summary>The Team handed to <c>TeamMembers</c>.</summary>
        [Parameter]
        public TeamSummary? Team { get; set; }

        /// <summary>The search text handed to <c>TeamMembers</c>.</summary>
        [Parameter]
        public string? Search { get; set; }

        /// <summary>Every value <c>TeamMembers</c> raised through <c>SearchChanged</c>, in order.</summary>
        public ConcurrentQueue<string?> Searches { get; } = new();

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<TeamMembers>(2);
            builder.AddComponentParameter(3, nameof(TeamMembers.Team), this.Team);
            builder.AddComponentParameter(4, nameof(TeamMembers.Search), this.Search);
            builder.AddComponentParameter(5, nameof(TeamMembers.SearchChanged), EventCallback.Factory.Create<string?>(this, this.Searches.Enqueue));
            builder.CloseComponent();
        }
    }
}
