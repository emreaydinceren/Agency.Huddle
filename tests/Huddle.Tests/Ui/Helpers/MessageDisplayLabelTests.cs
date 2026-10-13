using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins the two label helpers <see cref="MessageDisplay"/> gained for UX-R25: <c>TimeLabel</c>, which
/// reads a Message time in <c>now</c>'s offset and shows the day only when it is not today, and
/// <c>FileName</c>, which takes the last segment of either separator style.
/// </summary>
public sealed class MessageDisplayLabelTests
{
    /// <summary>The "now" the time cases are read against: Tuesday 13 October 2026 at 10:00, offset +02:00.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 10, 0, 0, TimeSpan.FromHours(2));

    /// <summary>A Message time reads as HH:mm on the same local day and as "ddd HH:mm" otherwise, in <c>now</c>'s offset.</summary>
    /// <param name="year">The Message year.</param>
    /// <param name="month">The Message month.</param>
    /// <param name="day">The Message day.</param>
    /// <param name="hour">The Message hour.</param>
    /// <param name="minute">The Message minute.</param>
    /// <param name="offsetHours">The Message's own offset in hours.</param>
    /// <param name="expected">The text the helper must return.</param>
    [Theory]
    [InlineData(2026, 10, 13, 7, 30, 0, "09:30")]
    [InlineData(2026, 10, 12, 22, 15, 0, "00:15")]
    [InlineData(2026, 10, 12, 21, 59, 0, "Mon 23:59")]
    [InlineData(2026, 10, 11, 12, 5, 0, "Sun 14:05")]
    public void TimeLabel_ShowsTheTimeAndTheDayOnlyWhenItIsNotToday(int year, int month, int day, int hour, int minute, int offsetHours, string expected)
    {
        DateTimeOffset at = new(year, month, day, hour, minute, 0, TimeSpan.FromHours(offsetHours));

        Assert.Equal(expected, MessageDisplay.TimeLabel(at, MessageDisplayLabelTests.Now));
    }

    /// <summary>A path reduces to its last segment for either separator; a null or empty path has no name.</summary>
    /// <param name="path">The path as the Tool call or the edit carries it.</param>
    /// <param name="expected">The file name the helper must return.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData(@"C:\docs\report.pdf", "report.pdf")]
    [InlineData("a/b/c.txt", "c.txt")]
    [InlineData("notes", "notes")]
    [InlineData("folder/", "folder/")]
    public void FileName_TakesTheLastSegment(string? path, string? expected)
    {
        Assert.Equal(expected, MessageDisplay.FileName(path));
    }
}
