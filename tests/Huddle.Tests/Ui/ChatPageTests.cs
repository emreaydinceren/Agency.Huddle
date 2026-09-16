using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders the Room page (<c>Chat.razor</c>) over a real, fully-composed application via
/// <see cref="TeamWebApplicationFactory"/>, the same way <see cref="AppHostTests"/> does. Each test
/// writes straight into the <see cref="Drafts"/> singleton the running app resolves - the same store
/// <c>AgentConnection</c> writes into off the pipe - so a Draft shows up exactly as it would for a
/// real streaming Turn, without needing a live circuit or a real Agent connection.
/// </summary>
public sealed class ChatPageTests
{
    /// <summary>A Draft's text and sender render on the Room page as it streams in, before any Message exists for it.</summary>
    [Fact]
    public async Task ChatPage_RendersADraft()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        drafts.Append(Guid.CreateVersion7().ToString("N"), room.Id, agent.Id, agent.Name, "Still thinking this through");

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Contains("Still thinking this through", html, StringComparison.Ordinal);
        Assert.Contains("streaming", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Once the real Message has arrived and its Draft is completed, the reply's text appears exactly
    /// once on the page - not once as a Draft and again as the Message.
    /// </summary>
    [Fact]
    public async Task ChatPage_ReplacesTheDraftWhenTheMessageArrives()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        var messageId = Guid.CreateVersion7().ToString("N");
        const string replyText = "The reply, now fully arrived";

        drafts.Append(messageId, room.Id, agent.Id, agent.Name, replyText);

        // The two steps AgentConnection performs when a real PostMessage with this id arrives: the
        // Message is persisted, and only then is the Draft that led up to it removed.
        await chat.PostAsync(room.Id, agent.Id, replyText, messageId, ct);
        drafts.Complete(messageId);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Equal(1, ChatPageTests.CountOccurrences(html, replyText));
    }

    /// <summary>A Stop button renders beside a live Draft, and nowhere on the page when there is no Draft.</summary>
    [Fact]
    public async Task ChatPage_ShowsStopWhileADraftIsLive_AndNotOtherwise()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        using var client = factory.CreateClient();

        var withoutDraft = await client.GetStringAsync($"/rooms/{room.Id}", ct);
        Assert.DoesNotContain("draft-stop", withoutDraft, StringComparison.Ordinal);

        drafts.Append(Guid.CreateVersion7().ToString("N"), room.Id, agent.Id, agent.Name, "Working on it");

        var withDraft = await client.GetStringAsync($"/rooms/{room.Id}", ct);
        Assert.Contains("draft-stop", withDraft, StringComparison.Ordinal);

        // Not the literal text "Stop": the button is an icon now, and a MudTooltip's own text only
        // materialises against MudPopoverProvider, so it is not reliably present in prerendered HTML.
        // aria-label is the reliable, accessible handle - see docs/agencyteam/testing.md.
        Assert.Contains("aria-label=\"Stop\"", withDraft, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Room header offers a Rename control seeded from the Room's current name. Rendered through
    /// <see cref="MudBunitContext"/> rather than a plain HTTP GET: the control sits behind a click
    /// (the Edit icon button), which is absent from a prerender - see <c>docs/agencyteam/testing.md</c>.
    /// </summary>
    [Fact]
    public async Task ChatPage_RoomHeader_OffersARenameControlSeededFromTheRoomName()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = ChatPageTests.NewContext(factory);
        var cut = ChatPageTests.RenderChatPage(ctx, room.Id);

        Assert.Contains("aria-label=\"Rename room\"", cut.Markup, StringComparison.Ordinal);

        cut.Find("button[aria-label=\"Rename room\"]").Click();

        var input = ChatPageTests.FindRoomNameInput(cut);
        Assert.Equal(room.Name, input.GetAttribute("value"));
    }

    /// <summary>Pressing Enter in the Rename control commits the typed name through <see cref="ChatService.RenameRoomAsync"/>.</summary>
    [Fact]
    public async Task ChatPage_Rename_EnterCommitsTheTypedNameThroughChatService()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = ChatPageTests.NewContext(factory);
        var cut = ChatPageTests.RenderChatPage(ctx, room.Id);

        cut.Find("button[aria-label=\"Rename room\"]").Click();
        var input = ChatPageTests.FindRoomNameInput(cut);
        await input.InputAsync("Pricing follow-up");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // The fact that matters is what ChatService actually wrote, not merely what the control shows -
        // a rename that only updated local component state would pass a markup-only assertion here.
        var renamed = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(renamed);
        Assert.Equal("Pricing follow-up", renamed.Name);

        // RenameRoomAsync publishes RoomsChanged, and Chat.razor's own subscriber reloads the Room off
        // that event rather than the commit handler re-fetching it itself - proving that path actually
        // repaints rather than merely trusting it does.
        while (!cut.Markup.Contains("Pricing follow-up", StringComparison.Ordinal))
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains("Pricing follow-up", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Escape leaves the Room's name untouched, both on screen and in the store.</summary>
    [Fact]
    public async Task ChatPage_Rename_EscapeLeavesTheNameUntouched()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = ChatPageTests.NewContext(factory);
        var cut = ChatPageTests.RenderChatPage(ctx, room.Id);

        cut.Find("button[aria-label=\"Rename room\"]").Click();
        var input = ChatPageTests.FindRoomNameInput(cut);
        await input.InputAsync("Something else entirely");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        var untouched = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(untouched);
        Assert.Equal(room.Name, untouched.Name);

        Assert.Contains($">{room.Name}<", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Something else entirely", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A <see cref="ChatException"/> from the rename call surfaces as a visible, announced alert - never silently.</summary>
    [Fact]
    public async Task ChatPage_Rename_AChatExceptionSurfacesAsAVisibleAlert()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = ChatPageTests.NewContext(factory);
        var cut = ChatPageTests.RenderChatPage(ctx, room.Id);

        cut.Find("button[aria-label=\"Rename room\"]").Click();
        var input = ChatPageTests.FindRoomNameInput(cut);

        // ChatService.RenameRoomAsync rejects a blank name with ErrorCodes.BadMessage - the same
        // ChatException every other door into this page already surfaces via ex.Message.
        await input.InputAsync("   ");
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        while (!cut.Markup.Contains("role=\"alert\"", StringComparison.Ordinal))
        {
            await Task.Delay(20, ct);
        }

        Assert.Contains("role=\"alert\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("A room name cannot be blank.", cut.Markup, StringComparison.Ordinal);

        var unchanged = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(unchanged);
        Assert.Equal(room.Name, unchanged.Name);
    }

    /// <summary>
    /// A tab opened mid-Turn must show the whole Draft rather than an orphaned suffix - Chat.razor
    /// reads <c>Drafts.ForRoom</c> in <c>LoadRoomAsync</c> for exactly this reason, rather than
    /// relying only on the live <c>DraftChanged</c> event a fresh page load never witnessed.
    /// </summary>
    [Fact]
    public async Task ChatPage_LoadsDraftsThatWereAlreadyInFlight()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        // Written before any request for the page is ever made, and with no DraftChanged
        // subscriber alive to have witnessed it - the only way this text can reach the page is
        // LoadRoomAsync reading the store directly.
        drafts.Append(Guid.CreateVersion7().ToString("N"), room.Id, agent.Id, agent.Name, "Already halfway through a long answer");

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Contains("Already halfway through a long answer", html, StringComparison.Ordinal);
    }

    /// <summary>A Stop is one of three normal ways a Turn ends, never a fault - the page must show no alert or error styling once one has happened.</summary>
    [Fact]
    public async Task ChatPage_ShowsNoAlertAfterAStop()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        var messageId = Guid.CreateVersion7().ToString("N");

        // Otherwise this Agent is offline by the same default TeammatesPageTests relies on (nothing
        // ever registers a real pipe connection), and the member-health strip (a separate, correct
        // alert) would fire for the wrong reason and make this assertion meaningless.
        factory.FakeAgentGateway.SetOnline(agent.Id);

        drafts.Append(messageId, room.Id, agent.Id, agent.Name, "Cut off partway through");

        // What a Stop looks like from this Room's point of view: the terminator arrives and the
        // Draft is gone, with no Message ever posted for it - IAgentGateway.StopTurnAsync itself
        // only asks the Agent's own connection to stop; the actual removal happens here, exactly
        // as it does for an ordinary completion.
        drafts.Complete(messageId);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        // Both the health strip and the budget prompt render through MudAlert now, so a bare
        // role="alert" check would no longer prove either is absent - it would also match one that
        // fired for an unrelated reason. Asserting on each strip's own class is the specific check.
        Assert.DoesNotContain("member-health-alert", html, StringComparison.Ordinal);
        Assert.DoesNotContain("composer-error", html, StringComparison.Ordinal);
        Assert.DoesNotContain("budget-prompt", html, StringComparison.Ordinal);
    }

    /// <summary>The tool the Agent is currently using renders as one muted line inside its Draft.</summary>
    [Fact]
    public async Task ChatPage_RendersToolActivity()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, drafts) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        var messageId = Guid.CreateVersion7().ToString("N");

        drafts.Activity(
            messageId, "call-1", room.Id, agent.Id, agent.Name, "Reading Persona.cs", ToolActivityStatus.InProgress);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Contains("tool-activity", html, StringComparison.Ordinal);
        Assert.Contains("Reading Persona.cs", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An Agent that is simply not running - no health report of any kind - produces no strip. This
    /// is the stock configuration, not an edge case: Team:Acp:Enabled is false by default, so no
    /// Persona has ever started and every Agent resolves to Offline. Listing those would put a
    /// permanent alert in every Room saying nothing the Teammate tile does not already show, and an
    /// alert that is always on is one nobody reads.
    /// </summary>
    [Fact]
    public async Task ChatPage_ShowsNoStripForAnAgentThatSimplyNeverStarted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        // Deliberately no PersonaHealth.Report: the Agent is offline because nothing ever started
        // it, which is absence, not failure.
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.DoesNotContain("member-health-alert", html, StringComparison.Ordinal);
    }

    /// <summary>An Offline Member's reason renders in the strip between the transcript and the composer, as an alert.</summary>
    [Fact]
    public async Task ChatPage_ShowsAStripForAnOfflineMember()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        // PersonaHealth is keyed by Persona Name, which - for an Agent backed by a real Persona -
        // is the same string as the Agent's own Name, exactly the mapping Teammates.razor already
        // relies on for the tile and card.
        var health = factory.Services.GetRequiredService<PersonaHealth>();
        health.Report(agent.Name, PersonaState.Offline, "The Adapter needs authentication.");

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains(agent.Name, html, StringComparison.Ordinal);
        Assert.Contains("The Adapter needs authentication.", html, StringComparison.Ordinal);
    }

    /// <summary>Every Member online and reporting no health problem shows no strip at all - the same rule the budget prompt already follows for a Stop and a spent Budget, neither of which may ever land here either.</summary>
    [Fact]
    public async Task ChatPage_ShowsNoStripWhenEveryMemberIsHealthy()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        factory.FakeAgentGateway.SetOnline(agent.Id);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        // The health strip is the only alert this scenario could wrongly produce (the Budget is
        // untouched, so budget-prompt cannot fire either) - asserting on its own class is the
        // specific check; a bare role="alert" would also match an unrelated MudAlert elsewhere on
        // the page and prove nothing about this strip.
        Assert.DoesNotContain("member-health-alert", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reproduces the bug in the task brief: the Composer's own status line is Component state that
    /// Chat.razor renders with no <c>@@key</c>, so navigating between Rooms reuses the same instance.
    /// Before the fix, <c>Composer.razor</c> only ever cleared <c>infoText</c>/<c>errorText</c> at the
    /// top of <c>SendAsync</c> - never on a Room change - so a confirmation from one Room stayed on
    /// screen in the next. Renders <see cref="Composer"/> directly through <see cref="MudBunitContext"/>
    /// (the same split <see cref="TeammatesPageTests"/> uses) and drives the Room switch the same way
    /// <see cref="InviteTeammateTests"/> drives its own state reset: a bUnit
    /// <c>SetParametersAndRender</c> with a new <c>RoomId</c>, exactly what Chat.razor does when the
    /// user navigates from one Room's page to another's.
    /// </summary>
    [Fact]
    public async Task Composer_StatusLine_DoesNotFollowARoomSwitch()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var (directory, chat, _) = ChatPageTests.Services(factory);

        var alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        var echo = await directory.UpsertAgentUserAsync("echo", null, ct);
        var gamma = await directory.UpsertAgentUserAsync("gamma", null, ct);
        Assert.NotNull(alpha);
        Assert.NotNull(echo);
        Assert.NotNull(gamma);

        var roomAlphaEcho = await chat.CreateRoomForAsync([alpha.Id, echo.Id], ct);
        var roomGamma = await chat.EnsureRoomForAsync(gamma, ct);

        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(chat);

        var cut = ctx.Render<Composer>(parameters => parameters.Add(p => p.RoomId, roomAlphaEcho.Id));

        // Same command the manual repro uses: inviting a real, uninvited agent into the current Room
        // produces the green confirmation line.
        await cut.InvokeAsync(() => cut.Instance.SendAsync("/invite @gamma"));

        Assert.Contains("composer-info", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Invited gamma.", cut.Markup, StringComparison.Ordinal);

        // The switch itself: Chat.razor changes the same Composer instance's RoomId parameter, with
        // no @key forcing a fresh one.
        cut.Render(parameters => parameters.Add(p => p.RoomId, roomGamma.Id));

        Assert.DoesNotContain("composer-info", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Invited gamma.", cut.Markup, StringComparison.Ordinal);
    }

    private static (ITeamDirectory Directory, ChatService Chat, Drafts Drafts) Services(TeamWebApplicationFactory factory)
    {
        return (
            factory.Services.GetRequiredService<ITeamDirectory>(),
            factory.Services.GetRequiredService<ChatService>(),
            factory.Services.GetRequiredService<Drafts>());
    }

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

    /// <summary>Registers this factory's real Room services into a fresh <see cref="MudBunitContext"/>, the same pattern <c>TeammatesPageTests</c> uses for <c>Teammates</c>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IChatStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ChatService>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<Drafts>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());

        // Chat.razor renders InviteTeammate as a child, which injects PersonaStore for its Team
        // filter - needed even though these tests never open that panel.
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        return ctx;
    }

    /// <summary>
    /// Renders the real <see cref="Chat"/> page for <paramref name="roomId"/>, with the popover
    /// provider <see cref="MudBunitContext.RenderWithPopovers"/> supplies so the Rename button's
    /// <c>MudTooltip</c> has somewhere to paint its content.
    /// </summary>
    private static IRenderedComponent<ContainerFragment> RenderChatPage(MudBunitContext ctx, string roomId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Chat>(0);
            builder.AddAttribute(1, nameof(Chat.RoomId), roomId);
            builder.CloseComponent();
        });

    /// <summary>The <c>&lt;input&gt;</c> under the Rename control's <c>MudTextField</c>, found by its Label the same way <c>TeammateCardTests</c> locates a field.</summary>
    private static IElement FindRoomNameInput(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-input-control")
            .First(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains("Room name", StringComparison.Ordinal)))
            .QuerySelector("input") ?? throw new InvalidOperationException("No input under the Room name field.");
}
