namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What started a Memory Tidy pass.</summary>
internal enum MemoryTidyStart
{
    /// <summary>The timetable came due.</summary>
    Timetable,

    /// <summary>The timetable was overdue: a <c>Run</c> decision with <c>Late</c> true.</summary>
    Overdue,

    /// <summary>The day after a pass ended at the Helper's daily cap.</summary>
    Continuation,

    /// <summary>The Human pressed Tidy now.</summary>
    TidyNow,

    /// <summary><c>Team:Memory:Tidy:RunAtStart</c>.</summary>
    RunAtStart,
}
