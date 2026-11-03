namespace Agency.Huddle.App.Jobs;

/// <summary>The days of the week a <see cref="JobCadence.Weekly"/> Job runs on, as flags so a record holding them compares by value.</summary>
[Flags]
internal enum JobWeekdays
{
    /// <summary>No day.</summary>
    None = 0,

    /// <summary>Monday.</summary>
    Monday = 1,

    /// <summary>Tuesday.</summary>
    Tuesday = 2,

    /// <summary>Wednesday.</summary>
    Wednesday = 4,

    /// <summary>Thursday.</summary>
    Thursday = 8,

    /// <summary>Friday.</summary>
    Friday = 16,

    /// <summary>Saturday.</summary>
    Saturday = 32,

    /// <summary>Sunday.</summary>
    Sunday = 64,
}
