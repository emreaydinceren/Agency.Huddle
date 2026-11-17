namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Memory:Tidy</c>: when the Memory Tidy pass runs and how much one call may hold (Memory Tidy spec §6.17).</summary>
public sealed class MemoryTidyOptions
{
    /// <summary>The default for <see cref="When"/>.</summary>
    public const string DefaultWhen = "weekly on sun at 03:00";

    /// <summary>The default for <see cref="KeepBackupDays"/>.</summary>
    public const int DefaultKeepBackupDays = 90;

    /// <summary>When a pass runs, in a Job's timetable words and this machine's zone. Only <c>weekly</c> and <c>daily</c> are accepted. An overdue pass runs once.</summary>
    public string When { get; set; } = DefaultWhen;

    /// <summary>Runs one pass thirty seconds after start-up. For tests and the manual page.</summary>
    public bool RunAtStart { get; set; }

    /// <summary>A folder with fewer topic files than this is not judged.</summary>
    public int MinFiles { get; set; } = 10;

    /// <summary>The most characters of files shown in full in one call.</summary>
    public int MaxInputChars { get; set; } = 60000;

    /// <summary>The most actions one Verdict may hold.</summary>
    public int MaxActions { get; set; } = 12;

    /// <summary>The most characters of <c>text</c> one Verdict may hold in all.</summary>
    public int MaxAnswerChars { get; set; } = 40000;

    /// <summary>The most files one Verdict may drop or mostly empty, for each folder. Zero lets the Helper drop nothing.</summary>
    public int MaxDropsPerFolder { get; set; } = 3;

    /// <summary>Backups older than this many days are deleted at the start of a pass. Zero or less keeps them all.</summary>
    public int KeepBackupDays { get; set; } = DefaultKeepBackupDays;
}
