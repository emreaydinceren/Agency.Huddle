namespace Agency.Huddle.App.Services;

/// <summary>How alarming a context fill is.</summary>
internal enum ContextFillBandKind
{
    /// <summary>Plenty of room left.</summary>
    Normal,

    /// <summary>The window is filling up; a compaction is near.</summary>
    Warning,

    /// <summary>The window is almost full.</summary>
    Error,
}
