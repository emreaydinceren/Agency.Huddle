using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>
/// Pins <see cref="NeedsYouTracker"/> against the table in Needs You spec §6.2, driven through a real
/// <see cref="ChatService"/>, <see cref="QuestionStore"/> and <see cref="ElicitationStore"/> over one
/// <see cref="RoomEvents"/>, so each test proves the tracker hears what the app really publishes.
/// </summary>
public sealed class NeedsYouTrackerTests
{
    /// <summary>An Agent Mentioning the Human lists the Room with a Mention reason, the Agent's Name and the Message time.</summary>
    [Fact]
    public async Task AgentMentionsHuman_ListsTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);

        ChatMessage posted = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You?", ct: ct);

        NeedsYouEntry entry = Assert.Single(fixture.Tracker.Snapshot());
        Assert.Equal(room.Id, entry.RoomId);
        Assert.Equal(NeedsYouReason.Mention, entry.Reasons);
        Assert.Equal("Nova", entry.AskedBy);
        Assert.Equal(posted.Timestamp, entry.Since);
    }

    /// <summary>An Agent Message that names nobody lists nothing, and raises no change.</summary>
    [Fact]
    public async Task AgentMessageWithoutMention_ListsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        int changes = 0;
        fixture.Tracker.Changed += () => changes++;

        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "working on it", ct: ct);

        Assert.Empty(fixture.Tracker.Snapshot());
        Assert.Equal(0, changes);
    }

    /// <summary>The Human posting in the Room ends a Mention.</summary>
    [Fact]
    public async Task HumanPosts_ClearsTheMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "postgres", ct: ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>A second Mention while the first is unanswered keeps the first one's time, because the Room has waited since then.</summary>
    [Fact]
    public async Task SecondMention_KeepsTheFirstTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        ChatMessage first = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You still there?", ct: ct);

        Assert.Equal(first.Timestamp, Assert.Single(fixture.Tracker.Snapshot()).Since);
    }

    /// <summary>Dismiss hides the Room, and a later Mention brings it back.</summary>
    [Fact]
    public async Task Dismiss_HidesUntilANewMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        fixture.Tracker.Dismiss(room.Id);
        Assert.Empty(fixture.Tracker.Snapshot());
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You, ping", ct: ct);

        Assert.Equal(room.Id, Assert.Single(fixture.Tracker.Snapshot()).RoomId);
    }

    /// <summary>Dismissing a Room does not touch the Question card behind it: dismissing a reminder is not answering.</summary>
    [Fact]
    public async Task Dismiss_LeavesTheQuestionCardInPlace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync("Fitness", [coach], ct);
        _ = fixture.Questions.TryPut(NeedsYouFixture.Card(room.Id, coach));

        fixture.Tracker.Dismiss(room.Id);

        Assert.Empty(fixture.Tracker.Snapshot());
        Assert.NotNull(fixture.Questions.Get(room.Id));
    }

    /// <summary>A Question card lists the Room with a Question reason and the card's own time, and the Human answering ends it.</summary>
    [Fact]
    public async Task QuestionCard_ListsTheRoom_UntilTheHumanPosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync("Fitness", [coach], ct);
        PendingQuestions card = NeedsYouFixture.Card(room.Id, coach);

        _ = fixture.Questions.TryPut(card);

        NeedsYouEntry entry = Assert.Single(fixture.Tracker.Snapshot());
        Assert.Equal(NeedsYouReason.Question, entry.Reasons);
        Assert.Equal(card.AskedAt, entry.Since);
        Assert.Equal("Coach", entry.AskedBy);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "Strength", ct: ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>With two Forms waiting, answering one leaves the Room listed; answering the second ends the Form reason.</summary>
    [Fact]
    public async Task Forms_StayListedUntilTheLastIsTaken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User claude = await fixture.AddAgentAsync("Claude", ct);
        Room room = await fixture.CreateRoomAsync("Docs", [claude], ct);
        PendingElicitation first = fixture.AddForm(room.Id, claude);
        PendingElicitation second = fixture.AddForm(room.Id, claude);
        Assert.Equal(NeedsYouReason.Form, Assert.Single(fixture.Tracker.Snapshot()).Reasons);

        _ = fixture.Forms.TryTake(room.Id, first.Id);
        Assert.Single(fixture.Tracker.Snapshot());

        _ = fixture.Forms.TryTake(room.Id, second.Id);
        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>A Room that spends its Budget is listed with a Budget reason, and the Human posting ends it.</summary>
    [Fact]
    public async Task SpentBudget_ListsTheRoom_UntilTheHumanPosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Research", [nova], ct);

        await SpendBudgetAsync(fixture, room, nova, ct);

        Assert.Equal(NeedsYouReason.Budget, Assert.Single(fixture.Tracker.Snapshot()).Reasons);

        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "carry on", ct: ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>Continue clears the Budget reason only: a Mention in the same Room stays, because Continue is not a reply.</summary>
    [Fact]
    public async Task Continue_ClearsBudget_ButNotMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Research", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "one", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "two", ct: ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "three, @You?", ct: ct);
        Assert.Equal(NeedsYouReason.Mention | NeedsYouReason.Budget, Assert.Single(fixture.Tracker.Snapshot()).Reasons);

        BudgetExtension extension = await fixture.Service.ExtendBudgetAsync(room.Id, ct);

        Assert.Equal(ExtendResult.Granted, extension.Result);
        Assert.Equal(NeedsYouReason.Mention, Assert.Single(fixture.Tracker.Snapshot()).Reasons);
    }

    /// <summary>One Room with a Mention and a Question is one row carrying both reasons.</summary>
    [Fact]
    public async Task MentionAndQuestion_AreOneRowWithBothReasons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);

        _ = fixture.Questions.TryPut(NeedsYouFixture.Card(room.Id, nova));

        Assert.Equal(NeedsYouReason.Mention | NeedsYouReason.Question, Assert.Single(fixture.Tracker.Snapshot()).Reasons);
    }

    /// <summary>The Room that has waited longest is first.</summary>
    [Fact]
    public async Task Snapshot_IsOldestFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room older = await fixture.CreateRoomAsync("Older", [nova], ct);
        Room newer = await fixture.CreateRoomAsync("Newer", [nova], ct);
        _ = await fixture.Service.PostAsync(newer.Id, nova.Id, "@You?", ct: ct);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await fixture.Service.PostAsync(older.Id, nova.Id, "@You?", ct: ct);

        IReadOnlyList<NeedsYouEntry> snapshot = fixture.Tracker.Snapshot();

        Assert.Equal([newer.Id, older.Id], snapshot.Select(entry => entry.RoomId).ToArray());
    }

    /// <summary>Deleting a Room removes its entry for good, and tells subscribers.</summary>
    [Fact]
    public async Task DeletedRoom_IsPruned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "@You?", ct: ct);
        TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Tracker.Changed += () => changed.TrySetResult();

        await fixture.Service.DeleteRoomAsync(room.Id, ct);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>A Room the Transcript scan found is listed, with the Mention's own time.</summary>
    [Fact]
    public async Task Seed_ListsAQuietRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        DateTimeOffset since = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

        fixture.Tracker.Seed("room-x", since, "Nova");

        NeedsYouEntry entry = Assert.Single(fixture.Tracker.Snapshot());
        Assert.Equal("room-x", entry.RoomId);
        Assert.Equal(since, entry.Since);
    }

    /// <summary>A live event already heard for a Room wins over the scan: the Human's reply is newer than the Transcript the scan read.</summary>
    [Fact]
    public async Task Seed_AfterALiveEvent_IsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, KnownIds.Human, "hello", ct: ct);

        fixture.Tracker.Seed(room.Id, DateTimeOffset.UnixEpoch, "Nova");

        Assert.Empty(fixture.Tracker.Snapshot());
    }

    /// <summary>Posts Agent Messages until the Room's Budget (3 in the fixture) is spent.</summary>
    /// <param name="fixture">The fixture.</param>
    /// <param name="room">The Room to spend.</param>
    /// <param name="agent">The posting Agent.</param>
    /// <param name="ct">Cancels the posts.</param>
    private static async Task SpendBudgetAsync(NeedsYouFixture fixture, Room room, User agent, CancellationToken ct)
    {
        for (int index = 0; index < 3; index++)
        {
            _ = await fixture.Service.PostAsync(room.Id, agent.Id, $"working {index}", ct: ct);
        }
    }
}
