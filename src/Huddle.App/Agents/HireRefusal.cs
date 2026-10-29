namespace Agency.Huddle.App.Agents;

/// <summary>A hire the limits refuse: which limit, how much of it is used and how many more would fit.</summary>
/// <param name="Kind">The limit that refused.</param>
/// <param name="Team">The Team that is full for <see cref="HireLimitKind.PerTeam"/>; <see langword="null"/> for the live-process limit.</param>
/// <param name="Used">Members of that Team, or live processes, before the hire.</param>
/// <param name="Limit">The limit's value.</param>
internal sealed record HireRefusal(HireLimitKind Kind, string? Team, int Used, int Limit)
{
    /// <summary>How many more hires fit before the limit.</summary>
    public int Fits => Math.Max(0, this.Limit - this.Used);
}
