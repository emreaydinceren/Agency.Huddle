using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>What a late, re-sent Turn sees (Turn Queue spec, section 5.8): the read to the end of the Transcript and the later block.</summary>
public sealed class LateTurnCatchUpTests
{
    private const string RoomId = "room-1";
    private const string Trigger = "t1";
    private const string ReplyId = "reply-1";

    private static readonly ChatMessage Before1 = Msg("b1", "Ana", "before one");
    private static readonly ChatMessage Before2 = Msg("b2", "Bo", "before two");
    private static readonly ChatMessage TriggerMessage = Msg(Trigger, "Human", "@Nova hi");
    private static readonly ChatMessage CleoLater = Msg("c1", "Cleo", "my answer");
    private static readonly ChatMessage WaitingTrigger = Msg("w1", "Human", "@Nova again");
    private static readonly ChatMessage RioLater = Msg("r1", "Rio", "later still");

    /// <summary>A re-sent Turn on a first Turn reads with its own reply id as the end and splits the answer at the trigger.</summary>
    [Fact]
    public async Task AddAsync_Redelivery_ReadsToTheEndWithTheReplyIdAndSplitsAtTheTrigger()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = Owner();
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem result = await step.AddAsync(Item(Redelivery.Waiting), false, ReplyId, [], ct);

        Assert.Equal(ReplyId, Assert.Single(owner.TranscriptReadCalls).BeforeMessageId);
        Assert.Equal(["b1", "b2"], Ids(result.Transcript?.Messages));
        Assert.Equal(["c1", "w1", "r1"], Ids(result.LaterMessages));
    }

    /// <summary>The later block leaves out this Agent's other waiting triggers in the Room.</summary>
    [Fact]
    public async Task AddAsync_Redelivery_DropsTheWaitingTriggersFromTheLaterMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TranscriptCatchUpStep step = Build(Owner());
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem result = await step.AddAsync(Item(Redelivery.CutOff), false, ReplyId, ["w1"], ct);

        Assert.Equal(["c1", "r1"], Ids(result.LaterMessages));
    }

    /// <summary>An ordinary first Turn reads up to its trigger and carries no later block.</summary>
    [Fact]
    public async Task AddAsync_Ordinary_ReadsBeforeTheTriggerAndHasNoLaterMessages()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = Owner();
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem result = await step.AddAsync(Item(null), false, ReplyId, ["w1"], ct);

        Assert.Equal(Trigger, Assert.Single(owner.TranscriptReadCalls).BeforeMessageId);
        Assert.Null(result.LaterMessages);
    }

    /// <summary>A re-sent Turn on a session already past its first Turn reads nothing and has no later block.</summary>
    [Fact]
    public async Task AddAsync_RedeliveryPastTheFirstTurn_ReadsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeRoomSessionOwner owner = Owner();
        TranscriptCatchUpStep step = Build(owner);
        step.MarkOpened(resumed: false, resumedAfterId: null);
        _ = await step.AddAsync(Item(null), false, "reply-0", [], ct);
        owner.TranscriptReadCalls.Clear();

        WorkItem result = await step.AddAsync(Item(Redelivery.Waiting), false, ReplyId, [], ct);

        Assert.Empty(owner.TranscriptReadCalls);
        Assert.Null(result.LaterMessages);
        Assert.Null(result.Transcript);
    }

    /// <summary>A long gap goes through the Catch-up token budget: the later Messages are trimmed too.</summary>
    [Fact]
    public async Task AddAsync_Redelivery_TrimsALongGapToTheTokenBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string big = new('x', 4000);
        List<ChatMessage> all = [Before1, TriggerMessage, Msg("g1", "Cleo", big), Msg("g2", "Cleo", big), Msg("g3", "Cleo", "small")];
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail("fake", roomId, all, 0)),
        };
        TranscriptCatchUpStep step = new(
            owner, new AcpOptions { TranscriptCatchUpTokens = 100 }, null, null, new FakePromptSource(), NullLogger.Instance, RoomId, static _ => false);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem result = await step.AddAsync(Item(Redelivery.Waiting), false, ReplyId, [], ct);

        Assert.Equal(["g3"], Ids(result.LaterMessages));
    }

    /// <summary>The before block and the later block share one token budget: each fits alone, together they stay within it, and the later block keeps priority.</summary>
    [Fact]
    public async Task AddAsync_Redelivery_BothBlocksShareOneTokenBudget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string mid = new('x', 120);
        List<ChatMessage> all =
        [
            Msg("b1", "Ana", mid), Msg("b2", "Ana", mid), TriggerMessage, Msg("g1", "Cleo", mid), Msg("g2", "Cleo", mid),
        ];
        FakeRoomSessionOwner owner = new()
        {
            ReadTranscriptHandler = (roomId, _, _, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail("fake", roomId, all, 0)),
        };
        TranscriptCatchUpStep step = new(
            owner, new AcpOptions { TranscriptCatchUpTokens = 100 }, null, null, new FakePromptSource(), NullLogger.Instance, RoomId, static _ => false);
        step.MarkOpened(resumed: false, resumedAfterId: null);

        WorkItem result = await step.AddAsync(Item(Redelivery.Waiting), false, ReplyId, [], ct);

        int combined = (result.Transcript?.Messages ?? []).Concat(result.LaterMessages ?? []).Sum(TokenEstimate.OfMessage);
        Assert.InRange(combined, 1, 100);
        Assert.Equal(["g1", "g2"], Ids(result.LaterMessages));
    }

    /// <summary>The prompt shows before-trigger Messages as Catch-up, the trigger once, then the later block under its header.</summary>
    [Fact]
    public void BuildPrompt_Redelivery_ShowsTheLaterBlockAfterTheMessage()
    {
        FakePromptSource prompts = new();
        WorkItem item = Item(Redelivery.Waiting) with
        {
            Transcript = new TranscriptCatchUp(false, [Before1, Before2], 0),
            LaterMessages = [CleoLater, RioLater],
        };

        string prompt = TurnPromptFramer.BuildPrompt(item, prompts);

        string expected =
            Render(prompts, "turn.transcriptHeader", ("{{roomLabel}}", "[Room: Room (id: room-1)]")) + "\n" +
            Line(prompts, "Ana", "before one") + "\n" + Line(prompts, "Bo", "before two") + "\n\n" +
            "[Room: Room (id: room-1)] Human: @Nova hi\n\n" +
            "Posted after that Message, while it waited:\n" +
            Line(prompts, "Cleo", "my answer") + "\n" + Line(prompts, "Rio", "later still");
        Assert.Equal(expected, prompt);
        Assert.Equal(1, CountOf(prompt, "@Nova hi"));
    }

    /// <summary>With nothing before the trigger the later block still follows the Message.</summary>
    [Fact]
    public void BuildPrompt_RedeliveryWithNothingBefore_StillShowsTheLaterBlock()
    {
        FakePromptSource prompts = new();
        WorkItem item = Item(Redelivery.Waiting) with { LaterMessages = [CleoLater] };

        string prompt = TurnPromptFramer.BuildPrompt(item, prompts);

        Assert.Equal(
            "[Room: Room (id: room-1)] Human: @Nova hi\n\nPosted after that Message, while it waited:\n" + Line(prompts, "Cleo", "my answer"),
            prompt);
    }

    /// <summary>An ordinary Turn and a Turn with no later Messages are byte-identical to a Turn without the property.</summary>
    [Fact]
    public void BuildPrompt_NoLaterMessages_IsUnchanged()
    {
        FakePromptSource prompts = new();
        const string Plain = "[Room: Room (id: room-1)] Human: @Nova hi";

        Assert.Equal(Plain, TurnPromptFramer.BuildPrompt(Item(null), prompts));
        Assert.Equal(Plain, TurnPromptFramer.BuildPrompt(Item(Redelivery.Waiting) with { LaterMessages = [] }, prompts));
    }

    /// <summary>The later header's default text is the catalog's and names no tool prefix.</summary>
    [Fact]
    public void Catalog_HoldsTheLaterHeaderWithoutAToolPrefix()
    {
        string text = PromptCatalog.Get("turn.laterHeader").Default;

        Assert.Equal("Posted after that Message, while it waited:", text);
        Assert.DoesNotContain("mcp__team__", text, StringComparison.Ordinal);
    }

    private static string[] Ids(IReadOnlyList<ChatMessage>? messages)
    {
        Assert.NotNull(messages);
        return [.. messages.Select(m => m.Id)];
    }

    private static int CountOf(string text, string part) => text.Split(part).Length - 1;

    private static string Line(FakePromptSource prompts, string sender, string text) =>
        prompts.Render("turn.catchUpLine", new Dictionary<string, string> { ["{{sender}}"] = sender, ["{{text}}"] = text });

    private static string Render(FakePromptSource prompts, string key, (string Name, string Value) value) =>
        prompts.Render(key, new Dictionary<string, string> { [value.Name] = value.Value });

    private static ChatMessage Msg(string id, string sender, string text) =>
        new(id, DateTimeOffset.UnixEpoch, "s-" + sender, sender, text);

    private static WorkItem Item(Redelivery? redelivery) =>
        new(RoomId, "Room", "Human", "@Nova hi", [], TriggerMessageId: Trigger, Redelivery: redelivery);

    private static FakeRoomSessionOwner Owner() => new()
    {
        ReadTranscriptHandler = (roomId, _, before, _, _) => Task.FromResult<TranscriptTail?>(new TranscriptTail(
            "fake",
            roomId,
            string.Equals(before, Trigger, StringComparison.Ordinal)
                ? [Before1, Before2]
                : [Before1, Before2, TriggerMessage, CleoLater, WaitingTrigger, RioLater],
            0)),
    };

    private static TranscriptCatchUpStep Build(FakeRoomSessionOwner owner) =>
        new(owner, new AcpOptions(), null, null, new FakePromptSource(), NullLogger.Instance, RoomId, static _ => false);
}
