namespace Agency.Huddle.App.Tasks;

/// <summary>
/// How the Tasks UI shows a date to a Human: <see cref="HumanDate"/> ("Today", "Fri", "Oct 27") with the full date as a
/// tooltip, resolved against the clock's local day. <see cref="Format"/> (<c>yyyy-MM-dd</c>) stays the format of the
/// date pickers and of every file or wire date; the Change log keeps its own local <c>yyyy-MM-dd HH:mm</c> format.
/// </summary>
internal static class TaskDates
{
    /// <summary>The invariant format string the date pickers and every stored date use.</summary>
    public const string Format = "yyyy-MM-dd";

    /// <summary>The human form of <paramref name="date"/> against the clock's local today, or an empty string when <see langword="null"/>.</summary>
    /// <param name="date">The date to show.</param>
    /// <param name="clock">The clock that says what today is.</param>
    public static string Human(DateOnly? date, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return date is { } value ? HumanDate.Format(value, TaskDates.Today(clock)) : string.Empty;
    }

    /// <summary>The human form of the local calendar day of <paramref name="date"/>, or an empty string when <see langword="null"/>.</summary>
    /// <param name="date">The timestamp to show.</param>
    /// <param name="clock">The clock that says what today is and which zone is local.</param>
    public static string Human(DateTimeOffset? date, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return date is { } value ? HumanDate.Format(TaskDates.LocalDay(value, clock), TaskDates.Today(clock)) : string.Empty;
    }

    /// <summary>The full date for a tooltip (<c>Tue, Oct 27, 2026</c>), or <see langword="null"/> when there is no date, so no <c>title</c> renders.</summary>
    /// <param name="date">The date to show.</param>
    public static string? Tooltip(DateOnly? date) => date is { } value ? HumanDate.Full(value) : null;

    /// <summary>The full local date for a tooltip, or <see langword="null"/> when there is no date.</summary>
    /// <param name="date">The timestamp to show.</param>
    /// <param name="clock">The clock whose local zone picks the calendar day.</param>
    public static string? Tooltip(DateTimeOffset? date, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return date is { } value ? HumanDate.Full(TaskDates.LocalDay(value, clock)) : null;
    }

    /// <summary>
    /// Whether <paramref name="task"/> is overdue: it has a due date before today in the clock's local
    /// time zone (as the Change log is local) and is not in a terminal state (UX-R08).
    /// </summary>
    /// <param name="task">The Task to test.</param>
    /// <param name="clock">The clock that says what today is.</param>
    public static bool IsOverdue(TaskItem task, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(clock);

        if (task.DueDate is not { } due || task.Status.IsTerminal())
        {
            return false;
        }

        return due < TaskDates.Today(clock);
    }

    /// <summary>The clock's local calendar day.</summary>
    /// <param name="clock">The clock.</param>
    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    /// <summary>The calendar day of <paramref name="value"/> in the clock's local zone.</summary>
    /// <param name="value">The timestamp.</param>
    /// <param name="clock">The clock whose zone is local.</param>
    private static DateOnly LocalDay(DateTimeOffset value, TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, clock.LocalTimeZone).DateTime);
}
