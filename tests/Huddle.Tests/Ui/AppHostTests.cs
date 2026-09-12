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