namespace Agency.Huddle.App.Services;

/// <summary>Configuration for the Turn Queue, bound from <c>Team:TurnQueue</c>.</summary>
public sealed class TurnQueueOptions
{
    /// <summary>Whether the queue is on. When false no row is written, nothing is re-sent, no line is posted and acks are ignored.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How old a waiting Message may be when it would be handed out. Zero or less means no limit.</summary>
    public TimeSpan ResumeWithin { get; set; } = TimeSpan.FromDays(1);

    /// <summary>How many days a <c>done</c> row is kept, for diagnosis.</summary>
    public int KeepDays { get; set; } = 7;
}
