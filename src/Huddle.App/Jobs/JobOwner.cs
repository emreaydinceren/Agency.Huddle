namespace Agency.Huddle.App.Jobs;

/// <summary>
/// Whose schedule a Job is on, which is also who it is for: an Agent, named by its Persona Name, or the Human. The Human is
/// not named, because the Human's Name can change and an Agent may be called <c>human</c>.
/// </summary>
/// <param name="AgentName">The Agent's Name, or <see langword="null"/> for the Human.</param>
internal sealed record JobOwner(string? AgentName)
{
    /// <summary>The Human's schedule.</summary>
    public static JobOwner Human { get; } = new((string?)null);

    /// <summary>Whether this is the Human's schedule.</summary>
    public bool IsHuman => this.AgentName is null;

    /// <summary>An Agent's schedule.</summary>
    /// <param name="name">The Agent's Persona Name.</param>
    /// <returns>The owner.</returns>
    public static JobOwner Agent(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new JobOwner(name);
    }

    /// <summary>Whether this owner is the Agent called <paramref name="name"/>, ignoring case as Names are compared everywhere.</summary>
    /// <param name="name">An Agent's Name.</param>
    /// <returns><see langword="true"/> when it is that Agent.</returns>
    public bool IsAgent(string name) => this.AgentName is { } own && string.Equals(own, name, StringComparison.OrdinalIgnoreCase);
}
