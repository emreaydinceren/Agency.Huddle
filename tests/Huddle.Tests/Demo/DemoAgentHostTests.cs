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

    /// <summary>
    /// ADR-0004: in a Direct Room (exactly the Human and one Agent) the demo agent answers every
    /// Message, not only ones that mention it, because there is nobody else the Message could be for.
    /// </summary>
    [Fact]
    public async Task DemoAgent_RepliesWhenNotMentioned_InTwoMemberRoom()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:DemoAgent:Enabled"] = "true",
                ["Team:DemoAgent:Names:0"] = "echo",
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

        await chat.PostAsync(roomId, KnownIds.Human, "hi there, no mention here", ct: ct);

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

    /// <summary>
    /// ADR-0004: in a Room with 3 or more Members the demo agent stays quiet unless Mentioned, so it
    /// does not talk over the other Agents there.
    /// </summary>
    [Fact]
    public async Task DemoAgent_DoesNotReply_WhenNotMentioned_InThreeMemberRoom()
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

        await chat.PostAsync(room.Id, KnownIds.Human, "hello everyone, no mention", ct: ct);

        // Bounded wait: give a (misbehaving) unmentioned demo agent every chance to reply before
        // asserting that it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Single(history);
    }

    /// <summary>
    /// ADR-0004: in a Room with 3 or more Members the demo agent still answers when Mentioned, and
    /// only the Mentioned one replies - the unmentioned one buffers the Message as Catch-up instead.
    /// </summary>
    [Fact]
    public async Task DemoAgent_RepliesWhenMentioned_InThreeMemberRoom()
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

        await chat.PostAsync(room.Id, KnownIds.Human, "@echo only, hi", ct: ct);

        IReadOnlyList<ChatMessage> history;
        while (true)
        {
            history = await store.ReadAllAsync(room.Id, ct);
            if (history.Count >= 2)
            {
                break;
            }

            await Task.Delay(100, ct);
        }

        // Bounded wait: give a (misbehaving) unmentioned alpha every chance to reply too before
        // asserting that it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        history = await store.ReadAllAsync(room.Id, ct);
        Assert.Equal(2, history.Count);
        Assert.StartsWith("**echo:**", history[^1].Text);
    }

    /// <summary>
    /// ReplyGate.Decide checks the Budget before the Mention, so a Mentioned demo agent in a Direct
    /// Room - normally an automatic reply on both counts - still stays quiet once the Room's Budget
    /// is spent. Delivered as a hand-built envelope via <see cref="RoomEvents"/> because a real Room's
    /// Budget never reaches exhaustion here through <see cref="ChatService.PostAsync"/> alone: with
    /// only the Human and one Agent in the Room, a Human Message always resets the Budget before the
    /// Agent could ever spend it down.
    /// </summary>
    [Fact]
    public async Task DemoAgent_DoesNotReply_WhenBudgetExhausted()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var fixture = await PipeHostFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Team:DemoAgent:Enabled"] = "true",
                ["Team:DemoAgent:Names:0"] = "echo",
            },
            ct);

        var directory = fixture.Services.GetRequiredService<ITeamDirectory>();
        var gateway = fixture.Services.GetRequiredService<IAgentGateway>();
        var store = fixture.Services.GetRequiredService<IChatStore>();
        var events = fixture.Services.GetRequiredService<RoomEvents>();

        User? echo = null;
        Room? room = null;
        while (echo is null || room is null)
        {
            echo = await directory.FindUserByNameAsync("echo", ct);
            if (echo is not null && gateway.IsOnline(echo.Id))
            {
                room = await directory.FindRoomWithExactMembersAsync(KnownIds.Human, echo.Id, ct);
            }

            if (room is null)
            {
                await Task.Delay(100, ct);
            }
        }

        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        var human = await directory.GetHumanAsync(ct);
        var message = new ChatMessage(
            Guid.CreateVersion7().ToString("N"), DateTimeOffset.UtcNow, human.Id, human.Name, $"@{echo.Name} hi");

        // Granted 1, Used 1: Exhausted. Mentions echo directly and the Room has only 2 Members, so the
        // Budget check is the only reason a reply would not follow.
        events.PublishMessagePosted(new MessagePostedEvent(room, message, members, [echo], new RoomBudget(1, 1)));

        // Bounded wait: give a (misbehaving) over-budget demo agent every chance to reply before
        // asserting that it did not.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        var history = await store.ReadAllAsync(room.Id, ct);
        Assert.Empty(history);
    }
}