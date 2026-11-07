using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Feed;

/// <summary>#428: a Turn refused because the Room's Budget was spent is kept as a Notification across a restart, with a Wake button, and nothing runs until the click.</summary>
public sealed class HeldMentionsTests
{
    /// <summary>Applies Feed commands straight to the store, so a test reads the result without waiting for the writer.</summary>
    private sealed class StoreFeed(FeedStore store, TimeProvider clock) : IFeedPublisher
    {
        public void Post(FeedMessage message) => store.Apply([new PostCommand(message, clock.GetUtcNow())]);

        public void Resolve(string targetKey, FeedSourceType type) => store.Apply([new ResolveCommand(targetKey, type, clock.GetUtcNow())]);
    }

    /// <summary>A Room with Ada, Bo and Cy, a Budget of three, and the pieces a keeper needs.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly FeedHarness harness;

        private Rig(NeedsYouFixture fixture, FeedHarness harness, Room room, User ada, User bo, User cy)
        {
            this.Fixture = fixture;
            this.harness = harness;
            this.Room = room;
            this.Ada = ada;
            this.Bo = bo;
            this.Cy = cy;
            this.Feed = new StoreFeed(harness.Store, fixture.Clock);
        }

        public NeedsYouFixture Fixture { get; }

        public Room Room { get; }

        public User Ada { get; }

        public User Bo { get; }

        public User Cy { get; }

        public StoreFeed Feed { get; }

        public IReadOnlyList<FeedItem> Open => this.harness.Store.Unresolved(FeedSourceType.RoomMentionHeld);

        public static async Task<Rig> CreateAsync(CancellationToken ct)
        {
            NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
            FeedHarness harness = new();
            await harness.Directory.InitializeAsync("You", ct);
            User ada = await fixture.AddAgentAsync("Ada", ct);
            User bo = await fixture.AddAgentAsync("Bo", ct);
            User cy = await fixture.AddAgentAsync("Cy", ct);
            Room room = await fixture.CreateRoomAsync("Merge", [ada, bo, cy], ct);
            return new Rig(fixture, harness, room, ada, bo, cy);
        }

        public HeldMentions Keeper(RoomEvents events, ChatService chat, Func<string, bool>? agentExists = null) => new(
            events, this.Feed, this.harness.Store, chat, this.Fixture.Team, this.Fixture.Transcript, agentExists ?? (_ => true),
            new FakePromptSource(), this.harness.Options, NullLogger<HeldMentions>.Instance);

        public HeldMentions Keeper(Func<string, bool>? agentExists = null) => this.Keeper(this.Fixture.Events, this.Fixture.Service, agentExists);

        /// <summary>Ada spends the Room's Budget of three; the third Message is the one that mentions <paramref name="mentions"/>.</summary>
        public async Task<ChatMessage> SpendBudgetAsync(string mentions, CancellationToken ct)
        {
            _ = await this.Fixture.Service.PostAsync(this.Room.Id, KnownIds.Human, "please sort this out", ct: ct);
            _ = await this.Fixture.Service.PostAsync(this.Room.Id, this.Ada.Id, "looking", ct: ct);
            _ = await this.Fixture.Service.PostAsync(this.Room.Id, this.Ada.Id, "still looking", ct: ct);
            return await this.Fixture.Service.PostAsync(this.Room.Id, this.Ada.Id, mentions + " please merge the branch", ct: ct);
        }

        /// <summary>A Chat service as a restarted app has it: same stores, a Budget that starts fresh.</summary>
        public (ChatService Chat, RoomEvents Events) Restarted()
        {
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            ChatService chat = new(
                this.Fixture.Team, this.Fixture.Transcript, events, new FakeMentionAliasSource(),
                TestOptions.Create(opts => opts.AgentMessageBudget = 3), new ProposalStore(events), this.Fixture.Questions,
                NullLogger<ChatService>.Instance, this.Fixture.Forms, timeProvider: this.Fixture.Clock);
            return (chat, events);
        }

        public void Seed(User agent, ChatMessage held) => this.Feed.Post(new FeedMessage(
            FeedSourceType.RoomMentionHeld,
            this.Room.Id + "|" + agent.Id,
            FeedActor.Huddle,
            true,
            new RoomMentionHeldPayload(this.Room.Id, this.Room.Name, null, agent.Name, agent.Id, held.Id)));

        public void Dispose()
        {
            this.harness.Dispose();
            this.Fixture.Dispose();
        }
    }

    /// <summary>The refused Turn records one Notification for each mentioned Agent, carrying the Room, the Agent and the Message.</summary>
    [Fact]
    public async Task RefusedTurn_RecordsOneNotificationPerRoomAndAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Keeper();

        ChatMessage held = await rig.SpendBudgetAsync("@Bo and @Cy", ct);

        List<RoomMentionHeldPayload> payloads = [.. rig.Open.Select(item => Assert.IsType<RoomMentionHeldPayload>(item.Payload))];
        Assert.Equal(2, payloads.Count);
        Assert.All(payloads, payload => Assert.Equal((rig.Room.Id, held.Id), (payload.RoomId, payload.MessageId)));
        Assert.Equal(["Bo", "Cy"], payloads.Select(payload => payload.Waiting).Order(StringComparer.Ordinal));
        Assert.All(rig.Open, item => Assert.True(item.Notify));
    }

    /// <summary>A mention that was delivered (Budget not spent), a Human's mention and a Message mentioning nobody record nothing.</summary>
    [Fact]
    public async Task OnlyAnAgentsMentionRefusedForBudget_IsRecorded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Keeper();

        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, rig.Ada.Id, "@Bo early, the Budget has room", ct: ct);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, KnownIds.Human, "@Cy hello", ct: ct);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, rig.Ada.Id, "no one mentioned", ct: ct);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, rig.Ada.Id, "still nobody", ct: ct);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, KnownIds.Human, "@Ada wrap up", ct: ct);

        Assert.Empty(rig.Open);
    }

    /// <summary>After a restart the Notification is kept and the startup pass posts nothing and raises no Message: no Turn runs.</summary>
    [Fact]
    public async Task Restart_KeepsTheNotification_AndRunsNoTurn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo", ct);
        rig.Seed(rig.Bo, held);
        (ChatService chat, RoomEvents events) = rig.Restarted();
        int raised = 0;
        events.MessagePosted += _ => Interlocked.Increment(ref raised);
        events.MessageRedelivered += _ => Interlocked.Increment(ref raised);
        HeldMentions keeper = rig.Keeper(events, chat);
        int before = (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count;

        await keeper.ReconcileAsync(ct);

        Assert.Equal(rig.Bo.Id, Assert.IsType<RoomMentionHeldPayload>(Assert.Single(rig.Open).Payload).AgentId);
        Assert.Equal(before, (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count);
        Assert.Equal(0, raised);
        Assert.False(keeper.IsHeld(rig.Room.Id));
    }

    /// <summary>The startup pass closes a Notification whose Agent is gone, whose Room is archived or whose Agent has acted since.</summary>
    [Fact]
    public async Task Restart_ClosesWhenAgentIsGone_RoomArchived_OrAgentActedSince()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo", ct);
        rig.Seed(rig.Bo, held);
        (ChatService chat, RoomEvents events) = rig.Restarted();

        await rig.Keeper(events, chat, name => !string.Equals(name, "Bo", StringComparison.Ordinal)).ReconcileAsync(ct);
        int afterGone = rig.Open.Count;

        rig.Seed(rig.Bo, held);
        await rig.Fixture.Team.SetRoomArchivedAsync(rig.Room.Id, true, ct);
        await rig.Keeper(events, chat).ReconcileAsync(ct);
        int afterArchived = rig.Open.Count;
        await rig.Fixture.Team.SetRoomArchivedAsync(rig.Room.Id, false, ct);

        rig.Seed(rig.Bo, held);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, KnownIds.Human, "carry on", ct: ct);
        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, rig.Bo.Id, "done, merged", ct: ct);
        await rig.Keeper(events, chat).ReconcileAsync(ct);

        Assert.Equal((0, 0, 0), (afterGone, afterArchived, rig.Open.Count));
    }

    /// <summary>One click posts exactly one Message that mentions the Agent, from the Human, and closes the Notification; a second click does nothing.</summary>
    [Fact]
    public async Task Wake_StartsExactlyOneTurn_ThenCloses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo", ct);
        rig.Seed(rig.Bo, held);
        (ChatService chat, RoomEvents events) = rig.Restarted();
        List<MessagePostedEvent> posted = [];
        events.MessagePosted += posted.Add;
        HeldMentions keeper = rig.Keeper(events, chat);
        string key = rig.Room.Id + "|" + rig.Bo.Id;

        string? first = await keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct);
        string? second = await keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct);

        Assert.Equal("Bo", first);
        Assert.Null(second);
        MessagePostedEvent one = Assert.Single(posted);
        Assert.Equal(KnownIds.Human, one.Message.SenderId);
        Assert.Equal(rig.Bo.Id, Assert.Single(one.Mentions).Id);
        Assert.False(one.Budget.Exhausted);
        Assert.Equal(
            $"@Bo a Message to you in this Room was held when its Budget ran out, and Huddle restarted before it reached you. Read Message {held.Id} and do what it asks of you.",
            one.Message.Text);
        Assert.DoesNotContain(rig.Open, item => item.TargetKey == key);
    }

    /// <summary>
    /// When the held Message mentioned two Agents, Wake on one starts only that Agent (the Message mentions it alone), the other
    /// keeps its Notification, a double click posts once, and the Human's Message resets the Room's Budget like any Human Message.
    /// </summary>
    [Fact]
    public async Task Wake_OfTwoMentionedAgents_WakesOnlyTheClickedOne_OnceAndResetsTheBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo and @Cy", ct);
        rig.Seed(rig.Bo, held);
        rig.Seed(rig.Cy, held);
        (ChatService chat, RoomEvents events) = rig.Restarted();
        _ = await chat.PostAsync(rig.Room.Id, rig.Ada.Id, "unrelated update", ct: ct);
        Assert.Equal(1, chat.GetBudget(rig.Room.Id).Used);
        List<MessagePostedEvent> posted = [];
        events.MessagePosted += posted.Add;
        HeldMentions keeper = rig.Keeper(events, chat);

        string?[] clicks = await Task.WhenAll(
            keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct),
            keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct));

        Assert.Single(clicks, click => click is not null);
        MessagePostedEvent one = Assert.Single(posted);
        Assert.Equal(KnownIds.Human, one.Message.SenderId);
        Assert.Equal([rig.Bo.Id], one.Mentions.Select(user => user.Id));
        Assert.Equal(0, chat.GetBudget(rig.Room.Id).Used);
        Assert.Equal(rig.Cy.Id, Assert.IsType<RoomMentionHeldPayload>(Assert.Single(rig.Open).Payload).AgentId);
    }

    /// <summary>Wake does nothing while the Room is still paused in memory, because Continue is the way then.</summary>
    [Fact]
    public async Task Wake_WhileTheRoomIsStillPaused_PostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo", ct);
        rig.Seed(rig.Bo, held);
        int before = (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count;
        HeldMentions keeper = rig.Keeper();

        string? woken = await keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct);

        Assert.Null(woken);
        Assert.True(keeper.IsHeld(rig.Room.Id));
        Assert.Equal(before, (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count);
        Assert.Single(rig.Open);
    }

    /// <summary>Wake for an Agent that is gone says nobody was woken, posts nothing and closes the Notification.</summary>
    [Fact]
    public async Task Wake_AgentGone_ClosesWithoutPosting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        ChatMessage held = await rig.SpendBudgetAsync("@Bo", ct);
        rig.Seed(rig.Bo, held);
        (ChatService chat, RoomEvents events) = rig.Restarted();
        int before = (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count;
        HeldMentions keeper = rig.Keeper(events, chat, _ => false);

        string? woken = await keeper.WakeAsync(rig.Room.Id, rig.Bo.Id, ct);

        Assert.Null(woken);
        Assert.Empty(rig.Open);
        Assert.Equal(before, (await rig.Fixture.Transcript.ReadAllAsync(rig.Room.Id, ct)).Count);
    }

    /// <summary>Before a restart, Continue delivers the held Message again and closes the Notification; a Human's mention closes it too.</summary>
    [Fact]
    public async Task ContinueBeforeRestart_Delivers_AndCloses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Keeper();
        _ = await rig.SpendBudgetAsync("@Bo", ct);
        Assert.Single(rig.Open);

        BudgetExtension extension = await rig.Fixture.Service.ExtendBudgetAsync(rig.Room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extension.Result);
        RedeliveryFacts redelivered = Assert.IsType<RedeliveryFacts>(extension.Redelivered);
        Assert.Equal(rig.Bo.Id, Assert.Single(redelivered.Mentions).Id);
        Assert.Empty(rig.Open);
    }

    /// <summary>A Human's mention of the held Agent starts its Turn through the ordinary door, so the Notification closes.</summary>
    [Fact]
    public async Task HumanMentionsTheAgent_ClosesTheNotification()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        _ = rig.Keeper();
        _ = await rig.SpendBudgetAsync("@Bo and @Cy", ct);

        _ = await rig.Fixture.Service.PostAsync(rig.Room.Id, KnownIds.Human, "@Bo go ahead", ct: ct);

        Assert.Equal("Cy", Assert.IsType<RoomMentionHeldPayload>(Assert.Single(rig.Open).Payload).Waiting);
    }

    /// <summary>Items saved before this kind existed still read, and the new payload round-trips.</summary>
    [Fact]
    public void Payloads_OldItemsStillRead_AndTheNewOneRoundTrips()
    {
        string legacyBudget = FeedPayloads.Serialize(FeedActorKind.Huddle, new RoomWaitPayload("room-1", "Plan", null));
        string legacyWake = """{"v":1,"actorKind":"huddle","data":{"id":"GRO-1","title":"T","team":"Growth","waiting":"Nova"}}""";
        string current = FeedPayloads.Serialize(FeedActorKind.Huddle, new RoomMentionHeldPayload("room-1", "Plan", "Growth", "Bo", "u-bo", "m-1"));

        Assert.IsType<RoomWaitPayload>(FeedPayloads.Read(legacyBudget, FeedSourceType.RoomBudget).Payload);
        Assert.Null(Assert.IsType<TaskWakesPausedPayload>(FeedPayloads.Read(legacyWake, FeedSourceType.TaskWakesPaused).Payload).RoomId);
        Assert.Equal(
            new RoomMentionHeldPayload("room-1", "Plan", "Growth", "Bo", "u-bo", "m-1"),
            FeedPayloads.Read(current, FeedSourceType.RoomMentionHeld).Payload);
        Assert.True(FeedSourceTypes.TryParse("room-mention-held", out FeedSourceType parsed));
        Assert.Equal(FeedSourceType.RoomMentionHeld, parsed);
    }
}
