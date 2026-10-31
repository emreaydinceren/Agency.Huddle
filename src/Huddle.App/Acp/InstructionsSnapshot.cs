namespace Agency.Huddle.App.Acp;

/// <summary>The Instructions an Agent starts a session with, from general to specific (Instructions spec §3.2).</summary>
/// <param name="Sections">One section per non-empty level, Huddle first and the Agent's own last.</param>
internal sealed record InstructionsSnapshot(IReadOnlyList<InstructionsSection> Sections)
{
    /// <summary>A snapshot with no sections, which the composer leaves out entirely.</summary>
    public static InstructionsSnapshot Empty { get; } = new([]);

    /// <summary>Whether no level has any text.</summary>
    public bool IsEmpty => this.Sections.Count == 0;
}
