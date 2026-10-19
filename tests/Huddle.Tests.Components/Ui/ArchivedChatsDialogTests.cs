namespace Agency.Huddle.Tests.Ui;

using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Factories;

/// <summary>
/// Renders <see cref="ArchivedChatsDialog"/> through the real <c>IDialogService</c> (the same door
/// <see cref="ArchivedChats"/> opens it through) alongside <see cref="RoomList"/>, so this proves the
/// end-to-end path the feature promises: a Room archived out of the sidebar shows up here, and
/// Unarchive puts it straight back - both live, through the same <see cref="RoomEvents.RoomsChanged"/>
/// both components subscribe to. See <see cref="RoomListTests"/> and <see cref="MudBunitContext"/>'s
/// own remarks for why a MudDialog needs <see cref="MudBunitContext.RenderWithPopovers"/> rather than a
/// plain <c>Render&lt;T&gt;</c>.
/// </summary>
public sealed class ArchivedChatsDialogTests
{
    /// <summary>The dialog's whole reason to exist: an archived Room is invisible in the sidebar but must still be listed here, with an Unarchive control.</summary>
    [Fact]
    public async Task Dialog_ListsAnArchivedRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);

        Assert.Contains(room.Name, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(cut.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Unarchive", StringComparison.Ordinal));
    }

    /// <summary>A Room that was never archived has nothing to do with this dialog.</summary>
    [Fact]
    public async Task Dialog_NonArchivedRoom_IsNotListed()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);

        Assert.Contains("No archived chats", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(room.Name, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The live end-to-end path: Unarchive from the dialog makes the Room reappear in the sidebar's
    /// own <see cref="RoomList"/>, rendered alongside the dialog in the same fragment - both react to
    /// the same <see cref="RoomEvents.RoomsChanged"/> publish from <see cref="ChatService.SetRoomArchivedAsync"/>.
    /// </summary>
    [Fact]
    public async Task Unarchive_ReturnsTheRoomToTheSidebar()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });
        Assert.DoesNotContain(room.Name, cut.Find("div.room-list").InnerHtml, StringComparison.Ordinal);

        var dialogService = ctx.Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(() => dialogService.ShowAsync<ArchivedChatsDialog>("Archived chats", ArchivedChatsDialog.Options));

        await cut.InvokeAsync(() => cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), "Unarchive", StringComparison.Ordinal)).ClickAsync());

        cut.WaitForAssertion(() => Assert.Contains(room.Name, cut.Find("div.room-list").InnerHtml, StringComparison.Ordinal));
        Assert.False((await directory.GetRoomAsync(room.Id, ct))?.Archived);
    }

    /// <summary>Delete from the dialog is guarded by the same inline confirm swap the sidebar's own row menu uses - one click must not remove the Room.</summary>
    [Fact]
    public async Task Delete_OneClick_DoesNotRemoveTheRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);
        await BeginDeleteAsync(cut);

        Assert.NotNull(await directory.GetRoomAsync(room.Id, ct));
        Assert.Contains(cut.FindAll("button"), button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal)); // contains-ok: collection membership of the verb button
    }

    /// <summary>Confirming Delete from the dialog permanently removes the Room, proven against the real <see cref="ITeamDirectory"/>.</summary>
    [Fact]
    public async Task Delete_Confirmed_RemovesTheRoom()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);
        await BeginDeleteAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());

        Assert.Null(await directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>Spec section 7: a Team-scoped Room's row reads "Room · Team", and a root Room's row keeps just its name.</summary>
    [Fact]
    public async Task Dialog_TeamScopedRoom_ReadsRoomDotTeam_AndRootRoomIsUnchanged()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var rootRoom = await chat.EnsureRoomForAsync(agent, ct);
        var teamRoom = await directory.CreateRoomAsync("Launch plan", [KnownIds.Human, agent.Id], "Marketing", false, ct);
        _ = await chat.SetRoomArchivedAsync(rootRoom.Id, archived: true, ct);
        _ = await chat.SetRoomArchivedAsync(teamRoom.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);

        string[] rows = [.. cut.FindAll(".archived-chats-row .flex-1-truncate").Select(element => element.TextContent.Trim())];
        Assert.Contains("Launch plan · Marketing", rows);
        Assert.Contains(rootRoom.Name, rows);
        Assert.DoesNotContain(rows, row => row.StartsWith(rootRoom.Name, StringComparison.Ordinal) && row.Contains('·'));
    }

    /// <summary>The Archived dialog's Delete confirm adds the Recycle Bin sentence with the file count when the Room has uploads (ChatUploads spec 7).</summary>
    [Theory]
    [InlineData(1, "Its uploaded file goes to the Recycle Bin.")]
    [InlineData(2, "Its 2 uploaded files go to the Recycle Bin.")]
    public async Task Delete_RoomWithUploads_ConfirmMentionsUploadCount(int count, string sentence)
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = count };
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);
        await BeginDeleteAsync(cut);

        Assert.Equal($"Delete \"{room.Name}\"? {sentence}", cut.Find(".inline-confirm-prompt").TextContent.Trim());
    }

    /// <summary>The Archived dialog's Delete confirm keeps the plain prompt when the Room has no uploads.</summary>
    [Fact]
    public async Task Delete_RoomWithoutUploads_ConfirmHasNoUploadSentence()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        chat.UploadFolders = new Agency.Huddle.Tests.Uploads.FakeRoomUploadFolders { Count = 0 };
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);
        await BeginDeleteAsync(cut);

        Assert.Equal($"Delete \"{room.Name}\"?", cut.Find(".inline-confirm-prompt").TextContent.Trim());
    }

    /// <summary>UX-E13: an archived Room's row says when it was archived ("Archived ..."), not when it was created.</summary>
    [Fact]
    public async Task Dialog_ArchivedRoom_ShowsArchivedDate_NotCreated()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var directory = await dir.CreateDirectoryAsync(ct);
        var agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        var chat = ChatServiceFactory.Create(directory, dir.Options(), events: events).Chat;
        var room = await chat.EnsureRoomForAsync(agent, ct);
        _ = await chat.SetRoomArchivedAsync(room.Id, archived: true, ct);

        await using var ctx = NewContext(directory, chat, events);
        var cut = await OpenDialogAsync(ctx);

        string date = cut.Find(".archived-chats-date").TextContent.Trim();
        Assert.Equal("Archived", date.Split(' ')[0]);
    }

    /// <summary>Opens the first row's menu and chooses Delete, which swaps the row for the inline confirm.</summary>
    private static async Task BeginDeleteAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find("button[aria-label^='Actions for ']").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir? dir = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(chat.TeamCatalog ?? new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir?.Options() ?? TestOptions.Create(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }

    /// <summary>Opens <see cref="ArchivedChatsDialog"/> directly through the real <c>IDialogService</c>, inside a fragment that already carries <see cref="MudBunitContext.RenderWithPopovers"/>'s dialog provider.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenDialogAsync(MudBunitContext ctx)
    {
        var cut = ctx.RenderWithPopovers(builder => { });

        var dialogService = ctx.Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(() => dialogService.ShowAsync<ArchivedChatsDialog>("Archived chats", ArchivedChatsDialog.Options));

        return cut;
    }
}
