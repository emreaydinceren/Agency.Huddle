namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Memory</c>, per Instructions and Memory §5.7.</summary>
public sealed class MemoryOptions
{
    /// <summary>The most bytes a memory file may hold after a <c>write</c>, <c>append</c> or <c>replace</c> through the memory tool; a larger result is refused. Zero or less means no cap.</summary>
    public int MaxFileBytes { get; set; } = 16384;

    /// <summary>The most characters a <c>propose_team_memory</c> fact may have (Team Memory Helper spec §6.1). Zero or less means no cap.</summary>
    public int MaxProposalChars { get; set; } = 1000;

    /// <summary>The most characters a <c>propose_team_memory</c> why may have. Zero or less means no cap.</summary>
    public int MaxProposalWhyChars { get; set; } = 300;

    /// <summary>The <c>reported.md</c> file of an Agent's own Memory, bound from <c>Team:Memory:Reported</c>.</summary>
    public MemoryReportedOptions Reported { get; set; } = new();
}
