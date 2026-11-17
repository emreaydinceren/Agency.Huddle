using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <c>C14</c> (Turn Queue spec §5.9): <see cref="WaitingNotice"/> says how many Messages wait for each Agent in the Room, offers
/// <c>Don't answer</c> only where the Agent has no Draft, names the held case, draws nothing when nothing waits, follows
/// <see cref="RoomEvents.TurnQueueChanged"/> and lets go of it on dispose.
/// </summary>
public sealed class WaitingNoticeTests
{
    /// <summary>Each Agent with waiting rows gets one status row with the exact count text, singular for one Message.</summary>
    [Fact]
    public async Task Renders_OneStatusRowPerAgentWithTheCountText()
    {
        await using MudBunitContext ctx = new();
        _ = Rig.Register(ctx, new WaitingCount("a1", "Nova", 2, false), new WaitingCount("a2", "Kai", 1, false));

        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);

        string[] texts = [.. cut.FindAll(".waiting-notice .waiting-notice-text").Select(static text => text.TextContent.Trim())];
        Assert.Equal(["Waiting for Nova: 2 Messages", "Waiting for Kai: 1 Message"], texts);
        Assert.All(cut.FindAll(".waiting-notice"), static row => Assert.Equal("status", row.GetAttribute("role")));
    }

    /// <summary>When the Room's Budget is spent the row says what the Messages wait for.</summary>
    [Fact]
    public async Task Held_AppendsTheBudgetClause()
    {
        await using MudBunitContext ctx = new();
        _ = Rig.Register(ctx, new WaitingCount("a1", "Nova", 3, true));

        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);

        Assert.Equal("Waiting for Nova: 3 Messages, until this Room's Budget is continued", cut.Find(".waiting-notice-text").TextContent.Trim());
    }

    /// <summary>With nothing waiting the component draws nothing.</summary>
    [Fact]
    public async Task NothingWaiting_RendersNothing()
    {
        await using MudBunitContext ctx = new();
        Rig rig = Rig.Register(ctx);

        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup) && rig.Control.Stops.Count == 0);
    }

    /// <summary>Don't answer is absent while the Agent has a Draft in the Room (its Stop is on the Draft) and present for an Agent without one.</summary>
    [Fact]
    public async Task DontAnswer_IsAbsentWhileTheAgentHasADraft()
    {
        await using MudBunitContext ctx = new();
        _ = Rig.Register(ctx, new WaitingCount("a1", "Nova", 2, false), new WaitingCount("a2", "Kai", 1, false));

        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,["a1"]);

        string[] buttons = [.. cut.FindAll("button").Select(static button => button.TextContent.Trim())];
        Assert.Equal(["Don't answer"], buttons);
        Assert.Equal("Waiting for Kai: 1 Message", cut.Find(".waiting-notice:has(button) .waiting-notice-text").TextContent.Trim());
    }

    /// <summary>Don't answer is offered for a held Agent too, and pressing it stops that Agent in this Room.</summary>
    [Fact]
    public async Task DontAnswer_Click_StopsThatAgentInThisRoom()
    {
        await using MudBunitContext ctx = new();
        Rig rig = Rig.Register(ctx, new WaitingCount("a1", "Nova", 2, true));
        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);

        await cut.InvokeAsync(() => cut.Find("button").ClickAsync());

        Assert.Equal(("a1", "r1"), Assert.Single(rig.Control.Stops));
    }

    /// <summary>A change to the Room's Turn Queue rereads the counts, and another Room's change does not.</summary>
    [Fact]
    public async Task TurnQueueChanged_RereadsOnlyForThisRoom()
    {
        await using MudBunitContext ctx = new();
        Rig rig = Rig.Register(ctx);
        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);
        rig.View.Counts = [new WaitingCount("a1", "Nova", 1, false)];

        rig.Events.PublishTurnQueueChanged("elsewhere");
        Assert.True(string.IsNullOrWhiteSpace(cut.Markup) && rig.Control.Stops.Count == 0);
        rig.Events.PublishTurnQueueChanged("r1");

        cut.WaitForAssertion(() => Assert.Equal("Waiting for Nova: 1 Message", cut.Find(".waiting-notice-text").TextContent.Trim()));
    }

    /// <summary>A re-render with an equal Draft set (the page builds a new set each render) does not read the store again; a changed set does.</summary>
    [Fact]
    public async Task ReRenderWithAnEqualDraftSet_DoesNotCallTheViewAgain()
    {
        await using MudBunitContext ctx = new();
        Rig rig = Rig.Register(ctx, new WaitingCount("a1", "Nova", 2, false));
        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx, ["a1"]);
        int afterFirstRender = rig.View.Calls;

        cut.Render(parameters => parameters.Add(p => p.RoomId, "r1").Add(p => p.AgentIdsWithDraft, new HashSet<string> { "a1" }));
        int afterEqualSet = rig.View.Calls;
        cut.Render(parameters => parameters.Add(p => p.RoomId, "r1").Add(p => p.AgentIdsWithDraft, new HashSet<string>()));

        Assert.Equal((1, 1, 2), (afterFirstRender, afterEqualSet, rig.View.Calls));
    }

    /// <summary>Disposing the component removes its subscription to the Turn Queue event, per rules.md.</summary>
    [Fact]
    public async Task Dispose_UnsubscribesFromTurnQueueChanged()
    {
        await using MudBunitContext ctx = new();
        Rig rig = Rig.Register(ctx);
        IRenderedComponent<WaitingNotice> cut = Rig.Render(ctx,[]);
        Assert.Equal(1, SubscriberCount(rig.Events));

        cut.Instance.Dispose();

        Assert.Equal(0, SubscriberCount(rig.Events));
    }

    /// <summary>How many handlers are subscribed to <see cref="RoomEvents.TurnQueueChanged"/>, read from the event's backing field.</summary>
    /// <param name="events">The hub to inspect.</param>
    private static int SubscriberCount(RoomEvents events)
    {
        System.Reflection.FieldInfo? field = typeof(RoomEvents).GetField("TurnQueueChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (field.GetValue(events) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>A fake view whose counts a test can change.</summary>
    private sealed class FakeView : ITurnQueueView
    {
        public IReadOnlyList<WaitingCount> Counts { get; set; } = [];

        public int Calls { get; private set; }

        public Task<IReadOnlyList<WaitingCount>> WaitingInAsync(string roomId, CancellationToken ct)
        {
            this.Calls++;
            return Task.FromResult(this.Counts);
        }
    }

    /// <summary>A fake control that records each Stop.</summary>
    private sealed class FakeControl : ITurnControl
    {
        public List<(string AgentId, string RoomId)> Stops { get; } = [];

        public Task StopAsync(string agentId, string roomId, CancellationToken ct)
        {
            this.Stops.Add((agentId, roomId));
            return Task.CompletedTask;
        }
    }

    private sealed class Rig
    {
        public RoomEvents Events { get; } = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<RoomEvents>.Instance);

        public FakeView View { get; } = new();

        public FakeControl Control { get; } = new();

        public static Rig Register(MudBunitContext ctx, params WaitingCount[] counts)
        {
            Rig rig = new();
            rig.View.Counts = counts;
            ctx.Services.AddSingleton(rig.Events);
            ctx.Services.AddSingleton<ITurnQueueView>(rig.View);
            ctx.Services.AddSingleton<ITurnControl>(rig.Control);
            return rig;
        }

        public static IRenderedComponent<WaitingNotice> Render(MudBunitContext ctx, HashSet<string> withDraft) =>
            ctx.Render<WaitingNotice>(parameters => parameters
                .Add(p => p.RoomId, "r1")
                .Add(p => p.AgentIdsWithDraft, withDraft));
    }
}
