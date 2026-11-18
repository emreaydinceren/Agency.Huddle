namespace Agency.Huddle.App.Goldfish;

/// <summary>Configuration for goldfish mode, bound from <c>Team:Goldfish</c>.</summary>
public sealed class GoldfishOptions
{
    /// <summary>Whether a possession may start. When false the start-up pass still ends any possession a restart left open.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many characters a brief may have.</summary>
    public int MaxBriefChars { get; set; } = 1000;

    /// <summary>How many days an ended possession is kept, for diagnosis.</summary>
    public int KeepDays { get; set; } = 30;
}
