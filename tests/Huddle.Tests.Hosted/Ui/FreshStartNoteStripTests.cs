using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Components.Chat;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Room-level fresh-start note on the Room page (Spec LR-R21, LR-R22; texts T12 to T16; correction C12): a
/// <c>role="status"</c> strip with the note, a "See what it reread" button opening a dialog with the reread text
/// in a <c>&lt;pre&gt;</c>, and a dismiss control. Rendered beside the context-only strip tests of
/// <see cref="ChatPageTests"/>, over the same composed application.
/// </summary>
public sealed class FreshStartNoteStripTests
{
    private const string AgentName = "alpha";
    private const string RereadText = "Alice: hello\nBob: the reread text, byte for byte";
    private const string StripSelector = "div.mud-alert.room-notice-column[role='status']";
    private const string DismissSelector = StripSelector + " button[aria-label='Dismiss']";
    private const string SeeButtonText = "See what it reread";

    /// <summary>A note held for this Room renders as a status strip in the notice column showing the note text exactly.</summary>
    [Fact]
    public async Task ChatPage_WithAFreshStartNote_ShowsTheNoteInAStatusStrip()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        FreshStartNote note = FreshStartNoteStripTests.Note(room.Id);
        factory.Services.GetRequiredService<FreshStartNotes>().Set(room.Id, AgentName, note);

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        IElement strip = cut.Find(StripSelector);
        Assert.Equal(FreshStartNoteText.Format(AgentName, note), FreshStartNoteStripTests.NoteLine(strip));
    }

    /// <summary>The "See what it reread" button opens a dialog titled "What alpha reread" whose body is the stored reread text in a pre element.</summary>
    [Fact]
    public async Task ChatPage_SeeWhatItReread_OpensTheDialogWithTheRereadText()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        factory.Services.GetRequiredService<FreshStartNotes>().Set(room.Id, AgentName, FreshStartNoteStripTests.Note(room.Id));
        factory.Services.GetRequiredService<LastCatchUpStore>().Write(AgentName, room.Id, RereadText);

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        FreshStartNoteStripTests.SeeButton(cut).Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".mud-dialog-title")), TimeSpan.FromSeconds(10));

        Assert.Equal("What alpha reread", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(RereadText, cut.Find(".mud-dialog-content pre").TextContent);
    }

    /// <summary>With no reread file the strip still shows but has no "See what it reread" button.</summary>
    [Fact]
    public async Task ChatPage_WithNoRereadFile_HasNoSeeWhatItRereadButton()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        factory.Services.GetRequiredService<FreshStartNotes>().Set(room.Id, AgentName, FreshStartNoteStripTests.Note(room.Id));

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        Assert.NotNull(cut.Find(StripSelector));
        Assert.DoesNotContain(SeeButtonText, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Dismissing the strip removes it and calls Dismiss: the store's json file is gone and the reread text stays.</summary>
    [Fact]
    public async Task ChatPage_DismissingTheStrip_RemovesItAndKeepsTheRereadText()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        FreshStartNotes notes = factory.Services.GetRequiredService<FreshStartNotes>();
        LastCatchUpStore store = factory.Services.GetRequiredService<LastCatchUpStore>();
        notes.Set(room.Id, AgentName, FreshStartNoteStripTests.Note(room.Id));
        store.Write(AgentName, room.Id, RereadText);
        string folder = Path.Combine(factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir, "room-sessions", "last-catch-up", AgentName);
        Assert.True(File.Exists(Path.Combine(folder, room.Id + ".json")));

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        cut.Find(DismissSelector).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(StripSelector)), TimeSpan.FromSeconds(10));
        Assert.Empty(notes.For(room.Id));
        Assert.False(File.Exists(Path.Combine(folder, room.Id + ".json")));
        Assert.True(File.Exists(Path.Combine(folder, room.Id + ".md")));
        Assert.Equal(RereadText, store.Read(AgentName, room.Id));
    }

    /// <summary>A note set and published while the page is open appears without a reload.</summary>
    [Fact]
    public async Task ChatPage_WhenFreshStartNotesChange_ShowsTheStripWithoutAReload()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);
        Assert.Empty(cut.FindAll(StripSelector));

        FreshStartNote note = FreshStartNoteStripTests.Note(room.Id);
        factory.Services.GetRequiredService<FreshStartNotes>().Set(room.Id, AgentName, note);
        factory.Services.GetRequiredService<RoomEvents>().PublishFreshStartNotesChanged(room.Id);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(StripSelector)), TimeSpan.FromSeconds(10));
        Assert.Equal(FreshStartNoteText.Format(AgentName, note), FreshStartNoteStripTests.NoteLine(cut.Find(StripSelector)));
    }

    /// <summary>A note that belongs to another Room is not shown here.</summary>
    [Fact]
    public async Task ChatPage_ANoteOfAnotherRoom_IsNotShown()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        factory.Services.GetRequiredService<FreshStartNotes>().Set("some-other-room", AgentName, FreshStartNoteStripTests.Note("some-other-room"));

        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        Assert.Empty(cut.FindAll(StripSelector));
        Assert.DoesNotContain("started afresh", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A note and reread file written before an app restart (fresh stores over the same Data Dir) still render the strip and a working button (C12).</summary>
    [Fact]
    public async Task ChatPage_AfterARestart_StillShowsTheStripAndItsButton()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        Room room = await FreshStartNoteStripTests.CreateRoomAsync(factory, ct);
        FreshStartNote note = FreshStartNoteStripTests.Note(room.Id);
        factory.Services.GetRequiredService<FreshStartNotes>().Set(room.Id, AgentName, note);
        factory.Services.GetRequiredService<LastCatchUpStore>().Write(AgentName, room.Id, RereadText);

        LastCatchUpStore restartedStore = new(factory.Services.GetRequiredService<IOptions<TeamOptions>>(), NullLogger<LastCatchUpStore>.Instance);
        FreshStartNotes restartedNotes = new(restartedStore);
        await using MudBunitContext ctx = FreshStartNoteStripTests.NewContext(factory);
        ctx.Services.AddSingleton(restartedStore);
        ctx.Services.AddSingleton(restartedNotes);
        IRenderedComponent<ContainerFragment> cut = FreshStartNoteStripTests.RenderChatPage(ctx, room.Id);

        Assert.Equal(FreshStartNoteText.Format(AgentName, note), FreshStartNoteStripTests.NoteLine(cut.Find(StripSelector)));
        FreshStartNoteStripTests.SeeButton(cut).Click();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".mud-dialog-content pre")), TimeSpan.FromSeconds(10));
        Assert.Equal(RereadText, cut.Find(".mud-dialog-content pre").TextContent);
    }

    /// <summary>The note's own line: the strip's text without the button label.</summary>
    private static string NoteLine(IElement strip) =>
        strip.TextContent.Replace(SeeButtonText, string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>The strip's "See what it reread" button.</summary>
    private static IElement SeeButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll(StripSelector + " button").First(static button => string.Equals(button.TextContent.Trim(), SeeButtonText, StringComparison.Ordinal));

    /// <summary>A fresh-start note that reread twelve Messages.</summary>
    private static FreshStartNote Note(string roomId) => new(roomId, "fresh", 12, 3400, false);

    /// <summary>Creates a Room with one Agent in it.</summary>
    private static async Task<Room> CreateRoomAsync(TeamWebApplicationFactory factory, CancellationToken ct)
    {
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        ChatService chat = factory.Services.GetRequiredService<ChatService>();
        User? agent = await directory.UpsertAgentUserAsync(AgentName, null, ct);
        Assert.NotNull(agent);
        return await chat.EnsureRoomForAsync(agent, ct);
    }

    /// <summary>A bUnit context with the services the Room page injects, taken from <paramref name="factory"/>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Chat));
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddFakeCircuitConnection();
        return ctx;
    }

    /// <summary>Renders the Room page for <paramref name="roomId"/> with the popover and dialog providers.</summary>
    private static IRenderedComponent<ContainerFragment> RenderChatPage(MudBunitContext ctx, string roomId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Chat>(0);
            builder.AddAttribute(1, nameof(Chat.RoomId), roomId);
            builder.CloseComponent();
        });
}
