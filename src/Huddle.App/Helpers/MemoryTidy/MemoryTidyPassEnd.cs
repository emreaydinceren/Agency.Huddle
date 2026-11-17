namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Why a Memory Tidy pass ended.</summary>
internal enum MemoryTidyPassEnd
{
    /// <summary>Every folder was looked at.</summary>
    Done,

    /// <summary>The Helper's daily cap was reached.</summary>
    Cap,

    /// <summary>Three calls in a row failed.</summary>
    ThreeFailures,

    /// <summary>The pass was stopped.</summary>
    Stopped,

    /// <summary>The pass threw.</summary>
    Threw,
}
