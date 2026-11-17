namespace Agency.Huddle.App.Services;

/// <summary>
/// The one place a Message's effect on a Room's Budget is worked out, so what <see cref="ChatService"/> stores
/// and what the owed-Turn policy is told cannot drift apart.
/// </summary>
internal static class RoomBudgets
{
    /// <summary>The Budget a Message leaves behind it.</summary>
    /// <param name="budget">The Room's Budget before the Message.</param>
    /// <param name="sender">Who sent the Message.</param>
    /// <param name="allowance">The Room's allowance, <see cref="TeamOptions.AgentMessageBudget"/>.</param>
    /// <returns>One more used for an Agent, a fresh <c>(0, allowance)</c> for the Human, <paramref name="budget"/> unchanged for Huddle.</returns>
    internal static RoomBudget After(RoomBudget budget, BudgetSender sender, int allowance)
    {
        ArgumentNullException.ThrowIfNull(budget);

        // A Human Message resets both halves: any extension the Human granted expires with the run it was granted for.
        return sender switch
        {
            BudgetSender.Agent => budget with { Used = budget.Used + 1 },
            BudgetSender.Human => new RoomBudget(0, allowance),
            _ => budget,
        };
    }
}
