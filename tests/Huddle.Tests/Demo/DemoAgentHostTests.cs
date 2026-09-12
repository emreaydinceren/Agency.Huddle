using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Pipes;

namespace Agency.Huddle.Tests.Demo;

/// <summary>
/// Startability tests for the built-in demo agent host (Team-Specifications.md §6.8, Appendix B.9). Each test
/// starts a real <see cref="Agency.Huddle.App"/> composition root over a unique pipe via <see cref="PipeHostFixture"/>,
/// with the demo agent options overridden through additional configuration.
/// </summary>
public sealed class DemoAgentHostTests
{
    [Fact]
    public async Task DemoAgentsEnabled_RegisterAndCreateRooms()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:DemoAgent:Enabled"] = "true",
                ["Team:DemoAgent:Names:0"] = "echo",
                ["Team:DemoAgent:Names:1"] = "alpha",
            },
            ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();

        IReadOnlyList<Room> rooms;
        while (true)
        {
            rooms = await directory.GetRoomsAsync(ct);
            var echo = await directory.FindUserByNameAsync("echo", ct);
            var alpha = await directory.FindUserByNameAsync("alpha", ct);

            if (rooms.Count == 2 && echo is not null && alpha is not null
                && gateway.IsOnline(echo.Id) && gateway.IsOnline(alpha.Id))
            {
                break;
            }

            await Task.Delay(100, ct);
        }

        Assert.Equal(2, rooms.Count);
        Assert.Contains(rooms, r => r.Name == "echo");
        Assert.Contains(rooms, r => r.Name == "alpha");
    }

    [Fact]
    public async Task DemoAgentsDisabled_NoRoomsCreated()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?> { ["Team:DemoAgent:Enabled"] = "false" },
            ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();

        // Bounded wait: give a (misbehaving) disabled demo agent host every chance to create rooms
        // before asserting that it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var rooms = await directory.GetRoomsAsync(ct);
        Assert.Empty(rooms);
    }

    [Fact]
    public async Task DemoAgent_RepliesWhenMentioned()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:DemoAgent:Enabled"] = "true",
                ["Team:DemoAgent:Names:0"] = "echo",
                ["Team:DemoAgent:Names:1"] = "alpha",
            },
            ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        string? roomId = null;
        while (roomId is null)
        {
            var echo = await directory.FindUserByNameAsync("echo", ct);
            if (echo is not null && gateway.IsOnline(echo.Id))
            {
                var room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
                roomId = room?.Id;
            }

            if (roomId is null)
            {
                await Task.Delay(100, ct);
            }
        }

        await chat.PostAsync(roomId, KnownIds.Human, "hi @echo", ct: ct);

        IReadOnlyList<ChatMessage> history = [];
        while (true)
        {
            history = await store.ReadAllAsync(roomId, ct);
            if (history.Count >= 2)
            {
                break;
            }

            await Task.Delay(100, ct);
        }

        Assert.Equal(2, history.Count);
        Assert.StartsWith("**echo:**", history[^1].Text);
    }

    [Fact]
    public async Task DemoAgent_DoesNotLoopWhenTwoAgentsAreMentioned()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:DemoAgent:Enabled"] = "true",
                ["Team:DemoAgent:Names:0"] = "echo",
                ["Team:DemoAgent:Names:1"] = "alpha",
            },
            ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();
        var chat = fixture.Services.GetRequiredService<ChatService>();
        var store = fixture.Services.GetRequiredService<IChatStore>();

        string? echoId = null;
        string? alphaId = null;
        while (echoId is null || alphaId is null)
        {
            var echo = await directory.FindUserByNameAsync("echo", ct);
            var alpha = await directory.FindUserByNameAsync("alpha", ct);
            if (echo is not null && alpha is not null && gateway.IsOnline(echo.Id) && gateway.IsOnline(alpha.Id))
            {
                echoId = echo.Id;
                alphaId = alpha.Id;
            }
            else
            {
                await Task.Delay(100, ct);
            }
        }

        var room = await chat.CreateRoomForAsync([echoId, alphaId], ct);

        await chat.PostAsync(room.Id, KnownIds.Human, "@echo @alpha hi", ct: ct);

        // Bounded wait: give a (misbehaving) looping pair of agents every chance to amplify before
        // asserting that they did not. Do not loop until some large count grows unbounded on disk.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var history = await store.ReadAllAsync(room.Id, ct);

        Assert.Equal(3, history.Count);
        Assert.Equal("@echo @alpha hi", history[0].Text);
        Assert.Contains(history, m => m.Text.StartsWith("**echo:**", StringComparison.Ordinal));
        Assert.Contains(history, m => m.Text.StartsWith("**alpha:**", StringComparison.Ordinal));
    }
}