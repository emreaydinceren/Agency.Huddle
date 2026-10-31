namespace Agency.Huddle.App.Components.Feed;

/// <summary>What one piece of a Feed summary is drawn as (Feed spec §6.12).</summary>
internal enum FeedSegmentKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Text in weight 600, such as a Team Name.</summary>
    Strong,

    /// <summary>A Task reference chip.</summary>
    Task,

    /// <summary>A Room reference chip.</summary>
    Room,

    /// <summary>A Library document reference chip.</summary>
    Document,

    /// <summary>A Helper reference chip, which opens its card on Settings > Helpers.</summary>
    Helper,

    /// <summary>A device reference chip, which opens Settings > Remote access.</summary>
    Device,
}
