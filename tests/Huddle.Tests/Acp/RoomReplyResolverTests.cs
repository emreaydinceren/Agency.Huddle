using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="RoomReplyResolver.Resolve"/>.</summary>
public sealed class RoomReplyResolverTests
{
    // Well clear of any Budget, so tests that are not about the Budget can ignore it.
    private static readonly RoomBudget Uncapped = new(0, 0);

    // Most of these tests are not about Following, so they pass an empty set.
    private static readonly HashSet<string> NoFollowers = new(StringComparer.Ordinal);

    private static User NewHuman(string id = KnownIds.Human) => new(id, "You", UserKind.Human, null);

    private static User NewAgent(string id) => new(id, id, UserKind.Agent, null);

    private static HashSet<string> ReachableIds(params string[] ids) => new(ids, StringComparer.Ordinal);

    /// <summary>A 2-member room replies without a Mention, because there is nobody else the Message could be for.</summary>
    [Fact]
    public void Resolve_TwoMembersAndTheHumanSpoke_ExpectsAReply()
    {
        User human = NewHuman();
        User agent = NewAgent("agent-1");

        RoomReply result = RoomReplyResolver.Resolve([human, agent], [], human.Id, ReachableIds(agent.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.Expected, result);
    }

    /// <summary>A 3-member room with nobody Mentioned is Context Only for every reachable teammate.</summary>
    [Fact]
    public void Resolve_ThreeMembersAndNoTeammateMentioned_IsContextOnly()
    {
        User human = NewHuman();
        User first = NewAgent("agent-1");
        User second = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, first, second], [], human.Id, ReachableIds(first.Id, second.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.ContextOnly, result);
    }

    /// <summary>A 3-member room where one teammate is Mentioned still expects a Reply, even though another is not.</summary>
    [Fact]
    public void Resolve_ThreeMembersAndOneMentioned_ExpectsAReply()
    {
        User human = NewHuman();
        User mentioned = NewAgent("agent-1");
        User other = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, mentioned, other], [mentioned], human.Id, ReachableIds(mentioned.Id, other.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.Expected, result);
    }

    /// <summary>A Mention does not buy a Reply past a spent Budget: the ordering is inherited from <see cref="ReplyGate"/>, never re-decided here.</summary>
    [Fact]
    public void Resolve_MentionedButBudgetSpent_IsPaused()
    {
        User human = NewHuman();
        User mentioned = NewAgent("agent-1");
        User bystander = NewHuman("human-2");

        RoomReply result = RoomReplyResolver.Resolve([human, mentioned, bystander], [mentioned], human.Id, ReachableIds(mentioned.Id), NoFollowers, new RoomBudget(Used: 2, Granted: 2));

        Assert.Equal(RoomReply.Paused, result);
    }

    /// <summary>A direct 2-member room where the sender is the Agent itself has nobody to wake: the sender is never a recipient.</summary>
    [Fact]
    public void Resolve_TheSenderIsNeverARecipient_SoADirectRoomHasNobodyToWake()
    {
        User human = NewHuman();
        User agent = NewAgent("agent-1");

        RoomReply result = RoomReplyResolver.Resolve([human, agent], [], agent.Id, ReachableIds(agent.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.NoRecipients, result);
    }

    /// <summary>A Mention of the Human, rather than a teammate, still reads as Context Only for every Agent.</summary>
    [Fact]
    public void Resolve_AMentionOfTheHuman_IsStillContextOnly()
    {
        User human = NewHuman();
        User first = NewAgent("agent-1");
        User second = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, first, second], [human], human.Id, ReachableIds(first.Id, second.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.ContextOnly, result);
    }

    /// <summary>One teammate Mentioned and one not still expects a Reply overall, because only one reachable recipient needs to say Reply.</summary>
    [Fact]
    public void Resolve_OneMentionedAndOneNot_ExpectsAReply()
    {
        User human = NewHuman();
        User mentioned = NewAgent("agent-1");
        User notMentioned = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, mentioned, notMentioned], [mentioned], human.Id, ReachableIds(mentioned.Id, notMentioned.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.Expected, result);
    }

    /// <summary>An uncapped Room - Granted zero or negative - can never resolve to Paused, no matter how many messages have been taken.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_AnUncappedRoom_IsNeverPaused(int granted)
    {
        User human = NewHuman();
        User first = NewAgent("agent-1");
        User second = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, first, second], [], human.Id, ReachableIds(first.Id, second.Id), NoFollowers, new RoomBudget(Used: 9999, Granted: granted));

        Assert.Equal(RoomReply.ContextOnly, result);
    }

    /// <summary>A Room with no Agent Members at all has nobody to wake.</summary>
    [Fact]
    public void Resolve_NoAgentMembersAtAll_HasNobodyToWake()
    {
        User human = NewHuman();
        User other = NewHuman("human-2");

        RoomReply result = RoomReplyResolver.Resolve([human, other], [], human.Id, ReachableIds(), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.NoRecipients, result);
    }

    /// <summary>Every teammate being unreachable resolves to NoRecipients, not ContextOnly - reachability is a recipient filter, not an outcome.</summary>
    [Fact]
    public void Resolve_EveryTeammateUnreachable_HasNobodyToWake()
    {
        User human = NewHuman();
        User first = NewAgent("agent-1");
        User second = NewAgent("agent-2");

        RoomReply result = RoomReplyResolver.Resolve([human, first, second], [], human.Id, ReachableIds(), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.NoRecipients, result);
    }

    /// <summary>
    /// Mentioning an unreachable teammate resolves to MentionedUnreachable, not NoRecipients and not
    /// ContextOnly: a teammate WAS named, so "nobody was addressed" (ContextOnly) is false, and "there
    /// was nobody to wake" (NoRecipients) is false in exactly the same way - this is its own outcome
    /// because both of the others would misdescribe a Message that plainly named someone.
    /// </summary>
    [Fact]
    public void Resolve_AMentionOfAnUnreachableTeammate_IsMentionedUnreachable()
    {
        User human = NewHuman();
        User offline = NewAgent("agent-1");

        RoomReply result = RoomReplyResolver.Resolve([human, offline], [offline], human.Id, ReachableIds(), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.MentionedUnreachable, result);
    }

    /// <summary>
    /// This is the case the MentionedUnreachable branch exists for: one Mentioned teammate is offline
    /// while a different teammate is online and reachable. Without the branch this would fall through
    /// to ContextOnly, and the Room would claim "no teammate was @-mentioned" when one plainly was.
    /// </summary>
    [Fact]
    public void Resolve_AMentionedTeammateOfflineWhileAnotherIsOnline_IsMentionedUnreachable()
    {
        User human = NewHuman();
        User echo = NewAgent("echo");
        User alpha = NewAgent("alpha");

        RoomReply result = RoomReplyResolver.Resolve([human, echo, alpha], [echo], human.Id, ReachableIds(alpha.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.MentionedUnreachable, result);
    }

    /// <summary>
    /// Pins the ordering inside <see cref="RoomReplyResolver.Resolve"/>: the Mention-reachability check
    /// runs before the fan-out that could otherwise answer Paused, so an unreachable Mention outranks a
    /// spent Budget too - the Human should learn the addressed teammate cannot answer, not that the
    /// Room merely ran out of Budget.
    /// </summary>
    [Fact]
    public void Resolve_AMentionedTeammateUnreachable_OutranksASpentBudget()
    {
        User human = NewHuman();
        User echo = NewAgent("echo");
        User alpha = NewAgent("alpha");

        RoomReply result = RoomReplyResolver.Resolve(
            [human, echo, alpha], [echo], human.Id, ReachableIds(alpha.Id), NoFollowers, new RoomBudget(Used: 2, Granted: 2));

        Assert.Equal(RoomReply.MentionedUnreachable, result);
    }

    /// <summary>
    /// <see cref="MessagePostedEvent.IsRecipient"/> excludes the sender from ever being a recipient, so
    /// an Agent that Mentions itself while itself unreachable must not trip the new branch - it resolves
    /// normally off whichever other, reachable teammate the Message also names.
    /// </summary>
    [Fact]
    public void Resolve_TheSenderBeingUnreachableIsNotAMentionedTeammate_StillResolvesNormally()
    {
        User human = NewHuman();
        User sender = NewAgent("echo");
        User other = NewAgent("alpha");

        RoomReply result = RoomReplyResolver.Resolve(
            [human, sender, other], [sender, other], sender.Id, ReachableIds(other.Id), NoFollowers, Uncapped);

        Assert.Equal(RoomReply.Expected, result);
    }

    /// <summary>
    /// A following recipient makes an otherwise context-only delivery resolve to Expected: nobody was
    /// Mentioned, but a follower's Reply Gate decision is Reply regardless, and one Reply is enough for
    /// the whole Room to resolve to Expected.
    /// </summary>
    [Fact]
    public void Resolve_AFollowingRecipient_TurnsAContextOnlyDeliveryIntoExpected()
    {
        User human = NewHuman();
        User follower = NewAgent("agent-1");
        User other = NewAgent("agent-2");
        HashSet<string> following = new([follower.Id], StringComparer.Ordinal);

        RoomReply result = RoomReplyResolver.Resolve([human, follower, other], [], human.Id, ReachableIds(follower.Id, other.Id), following, Uncapped);

        Assert.Equal(RoomReply.Expected, result);
    }
}
