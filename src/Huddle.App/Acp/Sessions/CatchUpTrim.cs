using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Trims a catch-up to a token budget in the drop order of LR-R11, as corrected by C6 and C7. Pass 1 drops the receiving Agent's own
/// Messages, oldest first, except its newest <c>keepOwn</c>, until the rest fits. Pass 2 drops the oldest remaining Messages of anyone,
/// protected own ones included (C6), until the rest fits. Own means <c>isOwn</c> (C7). The budget is a hard bound.
/// </summary>
internal static class CatchUpTrim
{
    /// <summary>
    /// Applies the two passes of LR-R11 (corrections C6 and C7) to a catch-up and returns what is kept.
    /// </summary>
    /// <param name="messages">The catch-up Messages, oldest first.</param>
    /// <param name="isOwn">Tells whether a Message is the receiving Agent's own (C7).</param>
    /// <param name="budgetTokens">The token budget the kept Messages must fit within.</param>
    /// <param name="keepOwn">How many of the newest own Messages pass 1 protects; must not be negative.</param>
    /// <returns>The kept Messages in input order, with the drop counts and the estimated tokens kept.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> or <paramref name="isOwn"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="keepOwn"/> is negative.</exception>
    internal static CatchUpTrimResult Apply(IReadOnlyList<ChatMessage> messages, Func<ChatMessage, bool> isOwn, int budgetTokens, int keepOwn)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(isOwn);
        ArgumentOutOfRangeException.ThrowIfNegative(keepOwn);

        int[] costs = new int[messages.Count];
        bool[] own = new bool[messages.Count];
        for (int index = 0; index < messages.Count; index++)
        {
            costs[index] = TokenEstimate.OfMessage(messages[index]);
            own[index] = isOwn(messages[index]);
        }

        bool[] dropped = new bool[messages.Count];
        int total = costs.Sum();
        total = DropOwnExceptNewest(costs, own, dropped, total, budgetTokens, keepOwn);
        total = DropOldest(costs, dropped, total, budgetTokens);

        return BuildResult(messages, own, dropped, total);
    }

    /// <summary>
    /// Pass 1 (LR-R11, C7): drops the oldest own Messages, never the newest <paramref name="keepOwn"/>, until the total fits.
    /// </summary>
    /// <param name="costs">The token cost of each Message.</param>
    /// <param name="own">Whether each Message is the receiving Agent's own.</param>
    /// <param name="dropped">Marks each Message dropped so far; updated in place.</param>
    /// <param name="total">The current total cost of the Messages not yet dropped.</param>
    /// <param name="budgetTokens">The token budget.</param>
    /// <param name="keepOwn">How many of the newest own Messages are protected.</param>
    /// <returns>The total cost after pass 1.</returns>
    private static int DropOwnExceptNewest(int[] costs, bool[] own, bool[] dropped, int total, int budgetTokens, int keepOwn)
    {
        List<int> ownIndices = [];
        for (int index = 0; index < own.Length; index++)
        {
            if (own[index])
            {
                ownIndices.Add(index);
            }
        }

        int droppable = Math.Max(0, ownIndices.Count - keepOwn);
        for (int position = 0; position < droppable && total > budgetTokens; position++)
        {
            total -= Drop(ownIndices[position], costs, dropped);
        }

        return total;
    }

    /// <summary>
    /// Pass 2 (LR-R11, C6): drops the oldest remaining Messages of anyone, protected own ones included, until the total fits.
    /// </summary>
    /// <param name="costs">The token cost of each Message.</param>
    /// <param name="dropped">Marks each Message dropped so far; updated in place.</param>
    /// <param name="total">The current total cost of the Messages not yet dropped.</param>
    /// <param name="budgetTokens">The token budget.</param>
    /// <returns>The total cost after pass 2.</returns>
    private static int DropOldest(int[] costs, bool[] dropped, int total, int budgetTokens)
    {
        for (int index = 0; index < dropped.Length && total > budgetTokens; index++)
        {
            if (!dropped[index])
            {
                total -= Drop(index, costs, dropped);
            }
        }

        return total;
    }

    /// <summary>
    /// Marks one Message as dropped.
    /// </summary>
    /// <param name="index">The index of the Message to drop.</param>
    /// <param name="costs">The token cost of each Message.</param>
    /// <param name="dropped">Marks each Message dropped so far; updated in place.</param>
    /// <returns>The token cost removed by this drop.</returns>
    private static int Drop(int index, int[] costs, bool[] dropped)
    {
        dropped[index] = true;
        return costs[index];
    }

    /// <summary>
    /// Builds the result: the kept Messages in input order, the drop counts and the estimated tokens that remain.
    /// </summary>
    /// <param name="messages">The catch-up Messages, oldest first.</param>
    /// <param name="own">Whether each Message is the receiving Agent's own.</param>
    /// <param name="dropped">Marks each Message dropped by either pass.</param>
    /// <param name="total">The total cost of the kept Messages.</param>
    /// <returns>The trim result.</returns>
    private static CatchUpTrimResult BuildResult(IReadOnlyList<ChatMessage> messages, bool[] own, bool[] dropped, int total)
    {
        List<ChatMessage> kept = [];
        int droppedCount = 0;
        int droppedOwnCount = 0;
        for (int index = 0; index < messages.Count; index++)
        {
            if (!dropped[index])
            {
                kept.Add(messages[index]);
                continue;
            }

            droppedCount++;
            if (own[index])
            {
                droppedOwnCount++;
            }
        }

        return new CatchUpTrimResult(kept, droppedCount, droppedOwnCount, total);
    }
}
