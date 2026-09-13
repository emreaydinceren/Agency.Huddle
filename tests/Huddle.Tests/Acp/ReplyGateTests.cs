using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

public sealed class ReplyGateTests
{
    // Well clear of any Budget, so these four pin the Room rule on its own.
    private const int Uncapped = 0;

    [Fact]
    public void TwoMembers_RepliesWithoutMention()
    {
        var result = ReplyGate.Decide(mentioned: false, memberCount: 2, agentMessagesSinceHuman: 0, budget: Uncapped);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    [Fact]
    public void ThreeMembers_RequiresMention()
    {
        var result = ReplyGate.Decide(mentioned: false, memberCount: 3, agentMessagesSinceHuman: 0, budget: Uncapped);

        Assert.Equal(ReplyDecision.CatchUp, result);
    }

    [Fact]
    public void ThreeMembers_MentionedReplies()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 0, budget: Uncapped);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    [Fact]
    public void OneMember_RepliesWithoutMention()
    {
        var result = ReplyGate.Decide(mentioned: false, memberCount: 1, agentMessagesSinceHuman: 0, budget: Uncapped);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    [Fact]
    public void OneBelowBudget_MentionedReplies()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 39, budget: 40);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    // The ordering rule: a Mention does not buy a Turn past the cap.
    [Fact]
    public void AtBudget_MentionedDoesNotReply()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 40, budget: 40);

        Assert.Equal(ReplyDecision.BudgetExhausted, result);
    }

    // Nor does a Room of two, where the Reply Gate otherwise always passes.
    [Fact]
    public void AtBudget_TwoMemberRoomDoesNotReply()
    {
        var result = ReplyGate.Decide(mentioned: false, memberCount: 2, agentMessagesSinceHuman: 40, budget: 40);

        Assert.Equal(ReplyDecision.BudgetExhausted, result);
    }

    // The two non-reply outcomes are not interchangeable: only CatchUp buffers the Message.
    [Fact]
    public void OverBudget_ReturnsBudgetExhaustedRatherThanCatchUp()
    {
        var result = ReplyGate.Decide(mentioned: false, memberCount: 3, agentMessagesSinceHuman: 41, budget: 40);

        Assert.Equal(ReplyDecision.BudgetExhausted, result);
    }

    [Fact]
    public void ZeroBudget_NeverExhausts()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 9999, budget: 0);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    [Fact]
    public void NegativeBudget_NeverExhausts()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 9999, budget: -1);

        Assert.Equal(ReplyDecision.Reply, result);
    }

    // An extension raises the Budget rather than resetting the count, so the same count that was
    // exhausted a moment ago must now pass. This is what makes the Continue button work at all.
    [Fact]
    public void ExtendedBudget_AllowsTheCountThatWasExhausted()
    {
        var result = ReplyGate.Decide(mentioned: true, memberCount: 3, agentMessagesSinceHuman: 40, budget: 80);

        Assert.Equal(ReplyDecision.Reply, result);
    }
}