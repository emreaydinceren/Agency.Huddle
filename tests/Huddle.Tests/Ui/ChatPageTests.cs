using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
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
        Assert.Contains("Stop", withDraft, StringComparison.Ordinal);
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

        drafts.Append(messageId, room.Id, agent.Id, agent.Name, "Cut off partway through");

        // What a Stop looks like from this Room's point of view: the terminator arrives and the
        // Draft is gone, with no Message ever posted for it - IAgentGateway.StopTurnAsync itself
        // only asks the Agent's own connection to stop; the actual removal happens here, exactly
        // as it does for an ordinary completion.
        drafts.Complete(messageId);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.DoesNotContain("role=\"alert\"", html, StringComparison.Ordinal);
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
}
