namespace Agency.Huddle.App.Tasks;

/// <summary>The Agent-wake budget for one Task (Spec §10.6): a guard against Agents looping.</summary>
/// <param name="Used">How many Agent-made wakes have been counted since the last Human change to the Task.</param>
/// <param name="Granted">The total wakes allowed before the budget is exhausted.</param>
public sealed record WakeBudget(int Used, int Granted)
{
    /// <summary>Whether the budget has no wakes left. A non-positive <see cref="Granted"/> never exhausts.</summary>
    public bool Exhausted => this.Granted > 0 && this.Used >= this.Granted;
}
