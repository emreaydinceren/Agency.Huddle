namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Outputs</c>, per Spec §7.4: the Outputs index cap.</summary>
public sealed class OutputsOptions
{
    /// <summary>The most Outputs index entries listed for an Agent, shared across the Agent's Teams (<c>Team:Outputs:MaxIndexEntries</c>).</summary>
    public int MaxIndexEntries { get; set; } = 40;
}
