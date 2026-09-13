using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Ui;

public sealed class AppHostTests
{
    [Fact]
    public async Task Root_Renders_EmptyState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("No rooms yet. Start an agent to create one.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_WithRoom_RendersRoomLinkInSidebar()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        var directory = factory.Services.GetRequiredService<ITeamDirectory>();
        var chat = factory.Services.GetRequiredService<ChatService>();

        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent!, ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/", ct);

        Assert.Contains($"href=\"/rooms/{room.Id}\"", html, StringComparison.Ordinal);
        Assert.Contains(">echo<", html, StringComparison.Ordinal);
    }

    // A page test sees only the prerender, which is exactly the path ChatService.GetBudget exists for:
    // this circuit never witnessed the Message that spent the Budget, and must still show the pause.
    [Fact]
    public async Task Room_AtBudget_RendersTheContinuePrompt()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var capped = factory.WithWebHostBuilder(b => b.UseSetting("Team:AgentMessageBudget", "1"));

        var directory = capped.Services.GetRequiredService<ITeamDirectory>();
        var chat = capped.Services.GetRequiredService<ChatService>();
        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        await chat.PostAsync(room.Id, agent.Id, "the message that spends it", ct: ct);

        using var client = capped.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.Contains("are paused", html, StringComparison.Ordinal);
        Assert.Contains("Continue", html, StringComparison.Ordinal);
        Assert.Contains("Leave paused", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Room_BelowBudget_RendersNoPrompt()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var capped = factory.WithWebHostBuilder(b => b.UseSetting("Team:AgentMessageBudget", "5"));

        var directory = capped.Services.GetRequiredService<ITeamDirectory>();
        var chat = capped.Services.GetRequiredService<ChatService>();
        var agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        var room = await chat.EnsureRoomForAsync(agent, ct);
        await chat.PostAsync(room.Id, agent.Id, "one of five", ct: ct);

        using var client = capped.CreateClient();
        var html = await client.GetStringAsync($"/rooms/{room.Id}", ct);

        Assert.DoesNotContain("are paused", html, StringComparison.Ordinal);
        Assert.Contains("1 of 5 agent replies since you last spoke", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_RendersNewChatControl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("New chat", html, StringComparison.Ordinal);
    }
}