using System.Globalization;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins <see cref="HumanDate"/>: Today, Tomorrow and Yesterday by name, a weekday for the next two to six days, and
/// a month and day otherwise (with the year when it is not this year).
/// </summary>
public sealed class HumanDateTests
{
    /// <summary>A Friday: 2026-10-16.</summary>
    private static readonly DateOnly Today = new(2026, 10, 16);

    /// <summary>The offset from today, and the text the date shows as.</summary>
    /// <returns>One row per boundary of the format.</returns>
    public static TheoryData<int, string> Offsets() => new()
    {
        { 0, "Today" },
        { 1, "Tomorrow" },
        { 2, "Sun" },
        { 6, "Thu" },
        { 7, "Oct 23" },
        { 30, "Nov 15" },
        { -1, "Yesterday" },
        { -2, "Oct 14" },
        { -16, "Sep 30" },
    };

    /// <summary>The offset from today, and the text a past moment's day shows as.</summary>
    /// <returns>One row per boundary of the past format.</returns>
    public static TheoryData<int, string> PastOffsets() => new()
    {
        { 0, "Today" },
        { -1, "Yesterday" },
        { -2, "Wed" },
        { -6, "Sat" },
        { -7, "Oct 9" },
        { 1, "Tomorrow" },
    };

    /// <summary>A moment that has happened reads as a weekday for two to six days back, not a date.</summary>
    /// <param name="offsetDays">Days from today.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [MemberData(nameof(PastOffsets))]
    public void FormatPast_RelativeToToday_ReadsWeekdaysBack(int offsetDays, string expected)
    {
        string text = HumanDate.FormatPast(Today.AddDays(offsetDays), Today);

        Assert.Equal(expected, text);
    }

    /// <summary>The activity log shows the local day and a 12-hour time, such as <c>Today 1:15 PM</c>.</summary>
    [Fact]
    public void HumanMoment_LocalDayAndTime_ReadsLikeTheActivityLog()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Fixed+02", TimeSpan.FromHours(2), "Fixed+02", "Fixed+02");
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 10, 16, 11, 30, 0, TimeSpan.Zero), zone);

        string today = TaskDates.HumanMoment(new DateTimeOffset(2026, 10, 16, 11, 15, 0, TimeSpan.Zero), clock);
        string earlier = TaskDates.HumanMoment(new DateTimeOffset(2026, 10, 12, 14, 20, 0, TimeSpan.Zero), clock);

        Assert.Equal("Today 1:15 PM", today);
        Assert.Equal("Mon 4:20 PM", earlier);
    }

    /// <summary>Each boundary around today formats as the settled rule says.</summary>
    /// <param name="offsetDays">Days from today.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [MemberData(nameof(Offsets))]
    public void Format_RelativeToToday_FollowsTheRule(int offsetDays, string expected)
    {
        string text = HumanDate.Format(Today.AddDays(offsetDays), Today);

        Assert.Equal(expected, text);
    }

    /// <summary>A date in another year carries the year, and the weekday and name rules still win across a year change.</summary>
    [Fact]
    public void Format_OtherYear_CarriesTheYear()
    {
        DateOnly newYearsEve = new(2026, 12, 30);

        Assert.Equal("Oct 27, 2025", HumanDate.Format(new DateOnly(2025, 10, 27), Today));
        Assert.Equal("Jan 6, 2027", HumanDate.Format(new DateOnly(2027, 1, 6), newYearsEve));
        Assert.Equal("Tue", HumanDate.Format(new DateOnly(2027, 1, 5), newYearsEve));
        Assert.Equal("Tomorrow", HumanDate.Format(new DateOnly(2026, 12, 31), newYearsEve));
        Assert.Equal("Yesterday", HumanDate.Format(new DateOnly(2026, 12, 29), newYearsEve));
    }

    /// <summary>A date this year, outside the named range, shows the month and day with no year.</summary>
    [Fact]
    public void Format_SameYearFarDate_HasNoYear()
    {
        Assert.Equal("Oct 27", HumanDate.Format(new DateOnly(2026, 10, 27), Today));
    }

    /// <summary>The tooltip form is the weekday, month, day and year.</summary>
    [Fact]
    public void Full_IsWeekdayMonthDayYear()
    {
        Assert.Equal("Tue, Oct 27, 2026", HumanDate.Full(new DateOnly(2026, 10, 27)));
    }

    /// <summary>Formatting ignores the thread culture, so a German machine still says "Oct".</summary>
    [Fact]
    public void Format_UsesTheInvariantCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("Oct 27", HumanDate.Format(new DateOnly(2026, 10, 27), Today));
            Assert.Equal("Tue, Oct 27, 2026", HumanDate.Full(new DateOnly(2026, 10, 27)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
