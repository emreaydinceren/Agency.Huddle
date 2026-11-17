using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>
/// <see cref="RoomBudgets.After"/> and <see cref="OwedTurnPolicy"/> (Turn Queue spec §5.4): the Budget a Message leaves and
/// which Agents owe a Turn for it, decided from the Reply Gate once, at post time.
/// </summary>
public sealed class OwedTurnPolicyTests
{
    private const string RoomId = "room-1";
    private static readonly User Human = new(KnownIds.Human, "You", UserKind.Human, null);
    private static readonly User Nova = new("nova", "Nova", UserKind.Agent, null);
    private static readonly User Kai = new("kai", "Kai", UserKind.Agent, null);
    private static readonly User Juno = new("juno", "Juno", UserKind.Agent, null);
    private static readonly Room TheRoom = new(RoomId, "Pricing", DateTimeOffset.UnixEpoch);
    private static readonly RoomBudget Fresh = new(0, 3);

    /// <summary>An Agent's Message leaves one more used and the same allowance.</summary>
    [Fact]
    public void After_Agent_AddsOneUsed()
    {
        RoomBudget after = RoomBudgets.After(new RoomBudget(1, 3), BudgetSender.Agent, 9);

        Assert.Equal(new RoomBudget(2, 3), after);
    }

    /// <summary>The Human's Message resets both halves, so an extension granted earlier expires.</summary>
    [Fact]
    public void After_Human_ResetsToTheAllowance()
    {
        RoomBudget after = RoomBudgets.After(new RoomBudget(5, 6), BudgetSender.Human, 3);

        Assert.Equal(new RoomBudget(0, 3), after);
    }

    /// <summary>Huddle's own Message neither spends nor resets the Budget.</summary>
    [Fact]
    public void After_Huddle_LeavesTheBudgetAlone()
    {
        RoomBudget before = new(2, 4);

        RoomBudget after = RoomBudgets.After(before, BudgetSender.Huddle, 9);

        Assert.Equal(before, after);
    }

    /// <summary>In a group Room a Mention owes only the Agent it names.</summary>
    [Fact]
    public void Owed_MentionInGroupRoom_OwesOnlyTheMentionedAgent()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai, Juno], mentions: [Kai]);

        Assert.Equal(["kai"], owed);
    }

    /// <summary>In a one-to-one Room the Agent owes a Turn without being Mentioned.</summary>
    [Fact]
    public void Owed_OneToOneRoom_OwesTheAgentWithoutAMention()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova], mentions: []);

        Assert.Equal(["nova"], owed);
    }

    /// <summary>An Agent that follows the Room owes a Turn without a Mention; one that does not follow, does not.</summary>
    [Fact]
    public void Owed_Follower_OwesWithoutAMention()
    {
        RoomFollows follows = new();
        _ = follows.Follow("juno", RoomId);

        IReadOnlyList<string> owed = Owed([Human, Nova, Kai, Juno], mentions: [], follows: follows);

        Assert.Equal(["juno"], owed);
    }

    /// <summary>A group Room's plain Message with no Mention and no follower is a CatchUp: nobody owes.</summary>
    [Fact]
    public void Owed_GroupRoomWithoutMentionOrFollower_OwesNobody()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai, Juno], mentions: []);

        Assert.Empty(owed);
    }

    /// <summary>The Agent a Message is withheld from owes nothing for it, though a one-to-one Room would otherwise owe.</summary>
    [Fact]
    public void Owed_WithheldAgent_OwesNothing()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova], mentions: [], withheldFromAgentId: "nova");

        Assert.Empty(owed);
    }

    /// <summary>Only the withheld Agent is skipped; another Agent that is Mentioned still owes.</summary>
    [Fact]
    public void Owed_WithheldAgent_DoesNotSkipTheOthers()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai], mentions: [Kai], withheldFromAgentId: "nova");

        Assert.Equal(["kai"], owed);
    }

    /// <summary>An Agent never owes a Turn for its own Message.</summary>
    [Fact]
    public void Owed_SenderItself_OwesNothing()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai], mentions: [Nova, Kai], senderId: "nova", budgetAfter: new RoomBudget(1, 3));

        Assert.Equal(["kai"], owed);
    }

    /// <summary>The Human is never an owed Agent, even Mentioned.</summary>
    [Fact]
    public void Owed_HumanMember_IsNeverOwed()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai, Juno], mentions: [Human], senderId: "nova", budgetAfter: new RoomBudget(1, 3));

        Assert.Empty(owed);
    }

    /// <summary>A spent Budget owes nobody, even a Mentioned Agent.</summary>
    [Fact]
    public void Owed_SpentBudget_OwesNobody()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai], mentions: [Kai], senderId: "nova", budgetAfter: new RoomBudget(3, 3));

        Assert.Empty(owed);
    }

    /// <summary>An Agent Message that is the one reaching the Budget owes nobody; the one before it still does.</summary>
    [Fact]
    public void Owed_AgentPostThatReachesTheBudget_OwesNobody()
    {
        RoomBudget before = new(2, 3);
        IReadOnlyList<string> last = Owed([Human, Nova, Kai], mentions: [Kai], senderId: "nova", budgetAfter: RoomBudgets.After(before, BudgetSender.Agent, 3));
        IReadOnlyList<string> earlier = Owed([Human, Nova, Kai], mentions: [Kai], senderId: "nova", budgetAfter: RoomBudgets.After(new RoomBudget(1, 3), BudgetSender.Agent, 3));

        Assert.Empty(last);
        Assert.Equal(["kai"], earlier);
    }

    /// <summary>A Human post into a Room whose Budget was spent is given the reset Budget and owes the Mentioned Agent.</summary>
    [Fact]
    public void Owed_HumanPostAfterExhaustedBudget_Owes()
    {
        RoomBudget after = RoomBudgets.After(new RoomBudget(3, 3), BudgetSender.Human, 3);

        IReadOnlyList<string> owed = Owed([Human, Nova, Kai], mentions: [Kai], budgetAfter: after);

        Assert.Equal(new RoomBudget(0, 3), after);
        Assert.Equal(["kai"], owed);
    }

    /// <summary>A Huddle post without a Mention is read as context: nobody owes, even in a one-to-one Room.</summary>
    [Fact]
    public void Owed_HuddlePostWithoutMention_OwesNobody()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova], mentions: [], senderId: KnownIds.Huddle, fromHuddle: true);

        Assert.Empty(owed);
    }

    /// <summary>A Huddle post that Mentions an Agent owes that Agent.</summary>
    [Fact]
    public void Owed_HuddlePostWithMention_OwesTheMentionedAgent()
    {
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai, Juno], mentions: [Nova], senderId: KnownIds.Huddle, fromHuddle: true);

        Assert.Equal(["nova"], owed);
    }

    /// <summary>The policy has no reachability filter: an Agent no pipe has ever connected still owes.</summary>
    [Fact]
    public void Owed_OfflineAgent_StillOwes()
    {
        // Nothing here supplies a reachable set or a gateway at all; a Mentioned Agent owes regardless of being online.
        IReadOnlyList<string> owed = Owed([Human, Nova, Kai], mentions: [Nova, Kai]);

        Assert.Equal(["nova", "kai"], owed);
    }

    /// <summary>The recipient loop both callers share keeps the Agents that are not the sender and, when asked, only the reachable ones.</summary>
    [Fact]
    public void RoomRecipients_Find_FiltersBySenderAndReachability()
    {
        List<User> all = RoomRecipients.Find([Human, Nova, Kai], "nova", null);
        List<User> reachable = RoomRecipients.Find([Human, Nova, Kai, Juno], KnownIds.Human, new HashSet<string>(StringComparer.Ordinal) { "kai" });

        Assert.Equal(["kai"], all.Select(user => user.Id));
        Assert.Equal(["kai"], reachable.Select(user => user.Id));
    }

    private static IReadOnlyList<string> Owed(
        IReadOnlyList<User> members,
        IReadOnlyList<User> mentions,
        string senderId = KnownIds.Human,
        RoomBudget? budgetAfter = null,
        string? withheldFromAgentId = null,
        bool fromHuddle = false,
        RoomFollows? follows = null)
    {
        OwedTurnPolicy policy = new(follows ?? new RoomFollows());
        return policy.Owed(TheRoom, members, mentions, senderId, budgetAfter ?? Fresh, withheldFromAgentId, fromHuddle);
    }
}
