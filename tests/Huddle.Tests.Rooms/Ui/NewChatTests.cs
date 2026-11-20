using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="NewChat"/>'s optional <c>Team</c> parameter (Team chats spec §6.5): with a Team the
/// panel is headed "New room in {Team}", lists the Team's Agents first and starts a Room scoped to the
/// Team; without one it behaves as it always did and starts a root Room (T7). Real
/// <see cref="SqliteTeamDirectory"/> and <see cref="ChatService"/> over a <see cref="TempDataDir"/>.
/// </summary>
public sealed class NewChatTests
{
    /// <summary>With a Team the panel is open at once and its heading names the Team.</summary>
    [Fact]
    public async Task WithTeam_PanelIsOpen_AndHeadingNamesTheTeam()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing");

        Assert.Equal("New room in Marketing", cut.Find(".new-chat-heading").TextContent.Trim());
    }

    /// <summary>With a Project the heading names the Team and the Project.</summary>
    [Fact]
    public async Task WithProject_HeadingNamesTheTeamAndTheProject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing", project: "Launch");

        Assert.Equal("New room in Marketing / Launch", cut.Find(".new-chat-heading").TextContent.Trim());
    }

    /// <summary>Choosing Start with a Project creates a Room that carries the Team and the Project.</summary>
    [Fact]
    public async Task WithProject_Start_CreatesARoomInTheProject()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova"], ["Nova"], ct);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing", project: "Launch");
        await cut.InvokeAsync(() => cut.Find(".new-chat-agents input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true }));

        await cut.InvokeAsync(() => cut.Find(".new-chat-start").ClickAsync());

        ITeamDirectory directory = ctx.Services.GetRequiredService<ITeamDirectory>();
        Room room = Assert.Single(await directory.GetRoomsForTeamAsync("Marketing", ct));
        Assert.Equal("Launch", room.Project);
    }

    /// <summary>Agents of the Team are listed before the others, whatever order the directory returns them in.</summary>
    [Fact]
    public async Task WithTeam_ListsTheTeamsMembersFirst()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Bo", "Ada", "Nova"], ["Nova"], ct);

        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing");

        string[] names = [.. cut.FindAll(".new-chat-agents .mud-checkbox").Select(box => box.TextContent.Trim())];
        Assert.Equal("Nova", names[0].Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)[^1]);
        Assert.Equal(3, names.Length);
    }

    /// <summary>T5: ticking an Agent and an outsider and choosing Start creates one Room scoped to the Team, leaves no root Room, and navigates to it.</summary>
    [Fact]
    public async Task WithTeam_Start_CreatesAScopedRoom_AndNavigates()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing");

        foreach (IElement box in cut.FindAll(".new-chat-agents input[type=checkbox]"))
        {
            await cut.InvokeAsync(() => box.ChangeAsync(new ChangeEventArgs { Value = true }));
        }

        await cut.InvokeAsync(() => cut.Find(".new-chat-start").ClickAsync());

        ITeamDirectory directory = ctx.Services.GetRequiredService<ITeamDirectory>();
        IReadOnlyList<Room> scoped = await directory.GetRoomsForTeamAsync("Marketing", ct);
        Room room = Assert.Single(scoped);
        Assert.Equal("Marketing", room.Team);
        Assert.DoesNotContain(await directory.GetRoomsAsync(ct), r => r.Team is null);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"rooms/{room.Id}", navigation.ToBaseRelativePath(navigation.Uri));
    }

    /// <summary>T7: without a Team the panel is headed "New room" once the "New room" link opens it, and Start creates the root Room (#720).</summary>
    [Fact]
    public async Task WithoutTeam_Start_CreatesARootRoom()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);
        IRenderedComponent<ContainerFragment> cut = Render(ctx, null);
        Assert.Empty(cut.FindAll(".new-chat-heading"));

        await cut.InvokeAsync(() => cut.Find(".nav-action-link .mud-nav-link").ClickAsync());
        Assert.Equal("New room", cut.Find(".new-chat-heading").TextContent.Trim());
        IElement nova = cut.FindAll(".new-chat-agents input[type=checkbox]")[0];
        await cut.InvokeAsync(() => nova.ChangeAsync(new ChangeEventArgs { Value = true }));
        await cut.InvokeAsync(() => cut.Find(".new-chat-start").ClickAsync());

        ITeamDirectory directory = ctx.Services.GetRequiredService<ITeamDirectory>();
        Room room = Assert.Single(await directory.GetRoomsAsync(ct));
        Assert.Null(room.Team);
    }

    /// <summary>The panel's Cancel button closes the panel and raises OnClosed once; it starts no chat, raises no OnStarted and does not navigate.</summary>
    [Fact]
    public async Task WithTeam_Cancel_ClosesThePanel_AndStartsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);
        int closed = 0;
        int started = 0;
        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing", () => closed++, () => started++);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        IElement nova = cut.FindAll(".new-chat-agents input[type=checkbox]")[0];
        await cut.InvokeAsync(() => nova.ChangeAsync(new ChangeEventArgs { Value = true }));

        await cut.InvokeAsync(() => cut.Find("button.new-chat-cancel").ClickAsync());

        Assert.Equal(1, closed);
        Assert.Equal(0, started);
        Assert.Empty(cut.FindAll(".new-chat-panel"));
        ITeamDirectory directory = ctx.Services.GetRequiredService<ITeamDirectory>();
        Assert.Empty(await directory.GetRoomsForTeamAsync("Marketing", ct));
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>A ChatException from the create call (the Team left the catalog meanwhile) is shown as an error snackbar; nothing is created, nothing navigates, the panel stays open with the selection kept and the circuit survives.</summary>
    [Fact]
    public async Task WithTeam_Start_ChatException_ShowsASnackbar_AndKeepsThePanelOpen()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        await using MudBunitContext ctx = await NewContextAsync(dir, ["Nova", "Bo"], ["Nova"], ct);
        int started = 0;
        IRenderedComponent<ContainerFragment> cut = Render(ctx, "Marketing", onStarted: () => started++);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        MudBlazor.ISnackbar snackbar = ctx.Services.GetRequiredService<MudBlazor.ISnackbar>();
        IElement nova = cut.FindAll(".new-chat-agents input[type=checkbox]")[0];
        await cut.InvokeAsync(() => nova.ChangeAsync(new ChangeEventArgs { Value = true }));
        ((FakeTeamCatalog)ctx.Services.GetRequiredService<ITeamCatalog>()).Teams = [];

        await cut.InvokeAsync(() => cut.Find(".new-chat-start").ClickAsync());

        MudBlazor.Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal("Unknown team 'Marketing'.", shown.Message);
        Assert.Equal(MudBlazor.Severity.Error, shown.Severity);
        Assert.Equal(before, navigation.Uri);
        Assert.Equal(0, started);
        Assert.Single(cut.FindAll(".new-chat-panel"));
        Assert.False(cut.Find(".new-chat-start").HasAttribute("disabled"));
        ITeamDirectory directory = ctx.Services.GetRequiredService<ITeamDirectory>();
        Assert.Empty(await directory.GetRoomsForTeamAsync("Marketing", ct));
    }

    private static async Task<MudBunitContext> NewContextAsync(TempDataDir dir, string[] agents, string[] members, CancellationToken ct)
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        foreach (string agent in agents)
        {
            _ = await directory.UpsertAgentUserAsync(agent, null, ct);
        }

        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Marketing", ["Launch"], members, true)] };
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        ChatService chat = new(directory, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance) { TeamCatalog = catalog };

        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<ITeamDirectory>(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<ITeamCatalog>(catalog);
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddPersonaStore(dir);
        return ctx;
    }

    private IRenderedComponent<ContainerFragment> Render(MudBunitContext ctx, string? team, Action? onClosed = null, Action? onStarted = null, string? project = null) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<NewChat>(0);
            if (team is not null)
            {
                builder.AddAttribute(1, nameof(NewChat.Team), team);
            }

            if (project is not null)
            {
                builder.AddAttribute(4, nameof(NewChat.Project), project);
            }

            if (onClosed is not null)
            {
                builder.AddAttribute(2, nameof(NewChat.OnClosed), EventCallback.Factory.Create(this, onClosed));
            }

            if (onStarted is not null)
            {
                builder.AddAttribute(3, nameof(NewChat.OnStarted), EventCallback.Factory.Create(this, onStarted));
            }

            builder.CloseComponent();
        });
}
