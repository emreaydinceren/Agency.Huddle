using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins U13 T13.3 (UX-R08, UX-043): <c>TaskDates.IsOverdue</c> compares the due date with "today" in the
/// clock's LOCAL time zone (the Change log is local too) and a terminal Task is never overdue.
/// </summary>
public sealed class TaskDatesOverdueTests
{
    /// <summary>A clock fixed at <paramref name="utc"/> whose local zone is <paramref name="offsetHours"/> hours from UTC.</summary>
    /// <param name="utc">The fixed instant.</param>
    /// <param name="offsetHours">The local zone's offset in hours.</param>
    private static ManualTimeProvider ClockAt(DateTimeOffset utc, int offsetHours)
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("test-zone", TimeSpan.FromHours(offsetHours), "test-zone", "test-zone");
        return new ManualTimeProvider(utc, zone);
    }

    /// <summary>At 23:30 UTC on the 24th it is already the 25th in UTC+2, so a Task due on the 24th is overdue; a UTC "today" would say it is not.</summary>
    [Fact]
    public void IsOverdue_LocalDayAheadOfUtc_DueYesterdayLocal_IsTrue()
    {
        ManualTimeProvider clock = ClockAt(new DateTimeOffset(2026, 9, 24, 23, 30, 0, TimeSpan.Zero), 2);
        TaskItem task = TestTasks.Make(status: TaskState.ToDo, dueDate: new DateOnly(2026, 9, 24));

        Assert.True(TaskDates.IsOverdue(task, clock));
    }

    /// <summary>At 03:00 UTC on the 25th it is still the 24th in UTC-8, so a Task due on the 24th is not overdue; a UTC "today" would say it is.</summary>
    [Fact]
    public void IsOverdue_LocalDayBehindUtc_DueTodayLocal_IsFalse()
    {
        ManualTimeProvider clock = ClockAt(new DateTimeOffset(2026, 9, 25, 3, 0, 0, TimeSpan.Zero), -8);
        TaskItem task = TestTasks.Make(status: TaskState.ToDo, dueDate: new DateOnly(2026, 9, 24));

        Assert.False(TaskDates.IsOverdue(task, clock));
    }

    /// <summary>A Task without a due date is never overdue.</summary>
    [Fact]
    public void IsOverdue_NoDueDate_IsFalse()
    {
        ManualTimeProvider clock = ClockAt(new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero), 0);
        TaskItem task = TestTasks.Make(status: TaskState.ToDo, dueDate: null);

        Assert.False(TaskDates.IsOverdue(task, clock));
    }

    /// <summary>A terminal Task is never overdue, however old its due date.</summary>
    [Theory]
    [InlineData(TaskState.Done)]
    [InlineData(TaskState.Cancelled)]
    [InlineData(TaskState.Duplicate)]
    [InlineData(TaskState.Rejected)]
    public void IsOverdue_TerminalState_IsFalse(TaskState status)
    {
        ManualTimeProvider clock = ClockAt(new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero), 0);
        TaskItem task = TestTasks.Make(status: status, dueDate: new DateOnly(2000, 1, 1));

        Assert.False(TaskDates.IsOverdue(task, clock));
    }

    /// <summary>An open Task is overdue from the day after its due date, including a Task in Needs You.</summary>
    [Theory]
    [InlineData(TaskState.Backlog)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.NeedsYou)]
    public void IsOverdue_OpenStatePastDue_IsTrue(TaskState status)
    {
        ManualTimeProvider clock = ClockAt(new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero), 0);
        TaskItem task = TestTasks.Make(status: status, dueDate: new DateOnly(2026, 9, 23));

        Assert.True(TaskDates.IsOverdue(task, clock));
    }
}
