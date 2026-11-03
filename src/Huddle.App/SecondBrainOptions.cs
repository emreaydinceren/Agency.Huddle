namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:SecondBrain</c>, per Spec §7.4: settings for the Second Brain check.</summary>
public sealed class SecondBrainOptions
{
    /// <summary>The number of days after which a Team counts as quiet for <c>brain-check</c> (<c>Team:SecondBrain:QuietDays</c>).</summary>
    public int QuietDays { get; set; } = 60;
}
