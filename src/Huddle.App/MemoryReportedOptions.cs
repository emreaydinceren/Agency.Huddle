namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Memory:Reported</c>: the <c>reported.md</c> file of an Agent's own Memory (Memory Tidy spec §6.1).</summary>
public sealed class MemoryReportedOptions
{
    /// <summary>The default for <see cref="KeepDays"/>.</summary>
    public const int DefaultKeepDays = 14;

    /// <summary>How many of the Human's local calendar days a line of <c>reported.md</c> is kept. Zero or less turns dropping off.</summary>
    public int KeepDays { get; set; } = DefaultKeepDays;
}
