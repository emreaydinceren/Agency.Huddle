using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Jobs;

/// <summary>What <see cref="IJobDestination.ResolveAsync"/> found: the Room to post into, or why the run stops.</summary>
/// <param name="Room">The Room, or <see langword="null"/> when the run stops.</param>
/// <param name="Outcome">The outcome that stops the run; <see cref="JobOutcome.Delivered"/> when there is a Room.</param>
internal sealed record JobTarget(Room? Room, JobOutcome Outcome)
{
    /// <summary>A Room to post into.</summary>
    /// <param name="room">The Room.</param>
    /// <returns>The target.</returns>
    public static JobTarget In(Room room) => new(room, JobOutcome.Delivered);

    /// <summary>No Room: the run stops with an outcome.</summary>
    /// <param name="outcome">Why: <see cref="JobOutcome.NoRoom"/>, <see cref="JobOutcome.NoAgent"/>, <see cref="JobOutcome.BudgetSpent"/> or <see cref="JobOutcome.Held"/>.</param>
    /// <returns>The target.</returns>
    public static JobTarget Stopped(JobOutcome outcome) => new(null, outcome);
}
