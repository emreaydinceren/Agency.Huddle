using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

public sealed class ReplyGateTests
{
    [Fact]
    public void TwoMembers_RepliesWithoutMention()
    {
        bool result = ReplyGate.ShouldReply(mentioned: false, memberCount: 2);

        Assert.True(result);
    }

    [Fact]
    public void ThreeMembers_RequiresMention()
    {
        bool result = ReplyGate.ShouldReply(mentioned: false, memberCount: 3);

        Assert.False(result);
    }

    [Fact]
    public void ThreeMembers_MentionedReplies()
    {
        bool result = ReplyGate.ShouldReply(mentioned: true, memberCount: 3);

        Assert.True(result);
    }

    [Fact]
    public void OneMember_RepliesWithoutMention()
    {
        bool result = ReplyGate.ShouldReply(mentioned: false, memberCount: 1);

        Assert.True(result);
    }
}