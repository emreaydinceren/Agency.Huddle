using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Acp.Sessions.CatchUpTrim"/>: the drop order of LR-R11 as a pure function (Spec section 3.3 and 5.3;
/// corrections C5 to C7). Every Message built here costs exactly 10 tokens under
/// <see cref="Agency.Huddle.App.Acp.Sessions.TokenEstimate"/>, so every budget below is a multiple of ten.
/// </summary>
public sealed class CatchUpTrimTests
{
    private const string NovaId = "nova-id";
    private const string MiraId = "mira-id";

    // 4 (Nova or Mira) + 2 separator characters + 24 text characters = 30, which costs 10 tokens.
    private const string FixedText = "abcdefghijklmnopqrstuvwx";

    private static readonly DateTimeOffset Stamp = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A trim where everything fits keeps every Message in order and drops nothing.</summary>
    [Fact]
    public void Apply_WithinBudget_KeepsEverything()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 4, otherCount: 2);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 100, keepOwn: 5);

        AssertTrim(actual, ["o1", "o2", "o3", "o4", "x1", "x2"], dropped: 0, droppedOwn: 0, estimatedTokens: 60);
    }

    /// <summary>Over budget, the oldest own Messages go first, and the newest keepOwn are protected, so the first pass alone is enough here.</summary>
    [Fact]
    public void Apply_OverBudgetWithinPassOne_DropsOldestOwnOnly()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 8, otherCount: 2);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 70, keepOwn: 5);

        AssertTrim(actual, ["o4", "o5", "o6", "o7", "o8", "x1", "x2"], dropped: 3, droppedOwn: 3, estimatedTokens: 70);
    }

    /// <summary>When the first pass cannot reach the budget, the second pass drops the oldest remaining Messages, protected own ones included.</summary>
    [Fact]
    public void Apply_OverBudgetAfterPassOne_DropsOldestRemainingIncludingProtectedOwn()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 8, otherCount: 4);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 40, keepOwn: 5);

        AssertTrim(actual, ["x1", "x2", "x3", "x4"], dropped: 8, droppedOwn: 8, estimatedTokens: 40);
    }

    /// <summary>With keepOwn 0 nothing is protected, so the first pass drops the oldest own Messages until the rest fits.</summary>
    [Fact]
    public void Apply_KeepOwnZero_ProtectsNoOwnMessage()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 8, otherCount: 2);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 40, keepOwn: 0);

        AssertTrim(actual, ["o7", "o8", "x1", "x2"], dropped: 6, droppedOwn: 6, estimatedTokens: 40);
    }

    /// <summary>With Messages interleaved, pass 2 drops the oldest remaining Message of anyone, not only own ones, and Kept keeps input order.</summary>
    [Fact]
    public void Apply_Interleaved_PassTwoDropsOldestOfAnyone()
    {
        IReadOnlyList<ChatMessage> messages =
        [
            new ChatMessage("x1", Stamp, MiraId, "Mira", FixedText),
            new ChatMessage("o1", Stamp, NovaId, "Nova", FixedText),
            new ChatMessage("x2", Stamp, MiraId, "Mira", FixedText),
            new ChatMessage("o2", Stamp, NovaId, "Nova", FixedText),
            new ChatMessage("x3", Stamp, MiraId, "Mira", FixedText),
            new ChatMessage("o3", Stamp, NovaId, "Nova", FixedText),
            new ChatMessage("o4", Stamp, NovaId, "Nova", FixedText),
        ];

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 30, keepOwn: 2);

        AssertTrim(actual, ["x3", "o3", "o4"], dropped: 4, droppedOwn: 2, estimatedTokens: 30);
    }

    /// <summary>An empty catch-up trims to nothing, with zero counts.</summary>
    [Fact]
    public void Apply_EmptyInput_ReturnsEmptyResult()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 0, otherCount: 0);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 100, keepOwn: 5);

        AssertTrim(actual, [], dropped: 0, droppedOwn: 0, estimatedTokens: 0);
    }

    /// <summary>A budget below the cost of the newest Message alone keeps nothing, and the counts cover every dropped Message.</summary>
    [Fact]
    public void Apply_BudgetBelowNewestMessage_KeepsNothing()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 2, otherCount: 1);

        Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual = Apply(messages, budgetTokens: 5, keepOwn: 1);

        AssertTrim(actual, [], dropped: 3, droppedOwn: 2, estimatedTokens: 0);
    }

    /// <summary>A null Message list is refused before any trim is attempted.</summary>
    [Fact]
    public void Apply_NullMessages_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Agency.Huddle.App.Acp.Sessions.CatchUpTrim.Apply(null!, static m => m.SenderId == NovaId, 100, 5));
    }

    /// <summary>A negative keepOwn is not a count, so it is refused rather than trimmed.</summary>
    [Fact]
    public void Apply_NegativeKeepOwn_ThrowsArgumentOutOfRange()
    {
        IReadOnlyList<ChatMessage> messages = Thread(ownCount: 1, otherCount: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Apply(messages, budgetTokens: 100, keepOwn: -1));
    }

    /// <summary>Runs the trim with Nova as the receiving Agent, so "own" means SenderId nova-id.</summary>
    /// <param name="messages">The catch-up Messages, oldest first.</param>
    /// <param name="budgetTokens">The token budget.</param>
    /// <param name="keepOwn">How many of the newest own Messages the first pass protects.</param>
    /// <returns>The trim result under test.</returns>
    private static Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult Apply(IReadOnlyList<ChatMessage> messages, int budgetTokens, int keepOwn)
    {
        return Agency.Huddle.App.Acp.Sessions.CatchUpTrim.Apply(messages, static m => m.SenderId == NovaId, budgetTokens, keepOwn);
    }

    /// <summary>Builds a thread with the own Messages first (o1 to o{ownCount}, from Nova) and then the others (x1 to x{otherCount}, from Mira).</summary>
    /// <param name="ownCount">How many Messages Nova sent.</param>
    /// <param name="otherCount">How many Messages Mira sent.</param>
    /// <returns>The Messages, oldest first.</returns>
    private static List<ChatMessage> Thread(int ownCount, int otherCount)
    {
        List<ChatMessage> messages = new();
        for (int index = 1; index <= ownCount; index++)
        {
            messages.Add(new ChatMessage($"o{index}", Stamp, NovaId, "Nova", FixedText));
        }

        for (int index = 1; index <= otherCount; index++)
        {
            messages.Add(new ChatMessage($"x{index}", Stamp, MiraId, "Mira", FixedText));
        }

        return messages;
    }

    /// <summary>Asserts the kept Message ids in order and every count of a trim result.</summary>
    /// <param name="actual">The trim result under test.</param>
    /// <param name="keptIds">The Ids expected in <c>Kept</c>, in order.</param>
    /// <param name="dropped">The expected <c>Dropped</c> count.</param>
    /// <param name="droppedOwn">The expected <c>DroppedOwn</c> count.</param>
    /// <param name="estimatedTokens">The expected <c>EstimatedTokens</c>.</param>
    private static void AssertTrim(Agency.Huddle.App.Acp.Sessions.CatchUpTrimResult actual, string[] keptIds, int dropped, int droppedOwn, int estimatedTokens)
    {
        Assert.Equal(keptIds, actual.Kept.Select(static m => m.Id));
        Assert.Equal(dropped, actual.Dropped);
        Assert.Equal(droppedOwn, actual.DroppedOwn);
        Assert.Equal(estimatedTokens, actual.EstimatedTokens);
    }
}
