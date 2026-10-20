namespace Agency.Huddle.App.Helpers;

/// <summary>The configuration of one Helper, bound from <c>Team:Helpers:&lt;Id&gt;</c>; a value left out keeps the Helper's code default.</summary>
public sealed class HelperOptions
{
    /// <summary>Whether the Helper runs.</summary>
    public bool? Enabled { get; set; }

    /// <summary>The Adapter id.</summary>
    public string? Adapter { get; set; }

    /// <summary>The Model id.</summary>
    public string? Model { get; set; }

    /// <summary>The Effort.</summary>
    public string? Effort { get; set; }

    /// <summary>Calls a day before the Helper pauses.</summary>
    public int? DailyCap { get; set; }

    /// <summary>Calls the Continue button adds for today.</summary>
    public int? ContinueGrant { get; set; }

    /// <summary>Seconds to wait for further saves of the same key before judging.</summary>
    public int? WaitSeconds { get; set; }

    /// <summary>Seconds one call may take.</summary>
    public int? TimeoutSeconds { get; set; }
}
