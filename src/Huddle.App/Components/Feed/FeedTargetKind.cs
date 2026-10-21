namespace Agency.Huddle.App.Components.Feed;

/// <summary>What kind of thing a Feed item is about.</summary>
internal enum FeedTargetKind
{
    /// <summary>The item says nothing this build can read.</summary>
    None,

    /// <summary>A Task.</summary>
    Task,

    /// <summary>A Room.</summary>
    Room,

    /// <summary>A Library document.</summary>
    Document,

    /// <summary>A Helper, opened on Settings > Helpers.</summary>
    Helper,
}
