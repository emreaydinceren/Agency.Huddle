namespace Agency.Huddle.App.Presence;

/// <summary>The four shapes a presence badge draws, so a state is never told by colour alone.</summary>
public enum PresenceGlyph
{
    /// <summary>A check: online, or working.</summary>
    Check,

    /// <summary>A pulse line: starting, or a Turn running (thinking).</summary>
    Pulse,

    /// <summary>A dash: degraded, or waiting.</summary>
    Dash,

    /// <summary>A hollow ring: offline, or idle.</summary>
    Ring,
}
