using Agency.Huddle.App.Data;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>Pins <see cref="NeedsYouRule.OldestUnansweredMention"/> (Needs You spec §6.1).</summary>
public sealed class NeedsYouRuleTests
{
    private static readonly Func<ChatMessage, bool> MentionsHuman = static message => message.Text.Contains("@You", StringComparison.Ordinal);

    /// <summary>An empty Transcript holds no Mention.</summary>
    [Fact]
    public void NoMessages_ReturnsNull()
    {
        Assert.Null(NeedsYouRule.OldestUnansweredMention([], NeedsYouRuleTests.MentionsHuman));
    }

    /// <summary>A Mention that the Human's own Message follows is answered.</summary>
    [Fact]
    public void HumanSpokeLast_ReturnsNull()
    {
        ChatMessage[] messages = [Agent("m1", "hi @You"), Human("m2", "hello")];

        Assert.Null(NeedsYouRule.OldestUnansweredMention(messages, NeedsYouRuleTests.MentionsHuman));
    }

    /// <summary>One Mention after the Human's last Message is returned.</summary>
    [Fact]
    public void OneMentionAfterHuman_IsReturned()
    {
        ChatMessage[] messages = [Human("m1", "go"), Agent("m2", "done, @You?")];

        ChatMessage? found = NeedsYouRule.OldestUnansweredMention(messages, NeedsYouRuleTests.MentionsHuman);

        Assert.Equal("m2", found?.Id);
    }

    /// <summary>Two unanswered Mentions give the older one, because the Room has waited since the first ask.</summary>
    [Fact]
    public void TwoMentionsAfterHuman_ReturnsTheOlder()
    {
        ChatMessage[] messages = [Human("m1", "go"), Agent("m2", "@You first"), Agent("m3", "no mention"), Agent("m4", "@You again")];

        ChatMessage? found = NeedsYouRule.OldestUnansweredMention(messages, NeedsYouRuleTests.MentionsHuman);

        Assert.Equal("m2", found?.Id);
    }

    /// <summary>A Mention before the Human's last Message is ignored, and so is a Message that mentions nobody.</summary>
    [Fact]
    public void MentionBeforeHuman_OrNoMention_ReturnsNull()
    {
        ChatMessage[] messages = [Agent("m1", "@You early"), Human("m2", "ok"), Agent("m3", "plain")];

        Assert.Null(NeedsYouRule.OldestUnansweredMention(messages, NeedsYouRuleTests.MentionsHuman));
    }

    private static ChatMessage Agent(string id, string text) => new(id, DateTimeOffset.UnixEpoch, "agent-1", "Nova", text);

    private static ChatMessage Human(string id, string text) => new(id, DateTimeOffset.UnixEpoch, KnownIds.Human, "You", text);
}
