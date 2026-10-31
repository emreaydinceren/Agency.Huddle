using Agency.Huddle.App.Skills.Ui;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins <see cref="ReadAgoText.Format"/> at its boundaries (Skill Packs spec §6.9.5).</summary>
public sealed class ReadAgoTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 16, 0, 0, TimeSpan.FromHours(2));

    /// <summary>Just now, 1 minute ago, N minutes ago, 1 hour ago, N hours ago, then a date.</summary>
    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(119, "1 minute ago")]
    [InlineData(120, "2 minutes ago")]
    [InlineData(3599, "59 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(7200, "2 hours ago")]
    [InlineData(86399, "23 hours ago")]
    [InlineData(86400, "Yesterday")]
    [InlineData(864000, "Oct 7")]
    public void Format_Boundaries(int secondsAgo, string expected)
    {
        Assert.Equal(expected, ReadAgoText.Format(Now.AddSeconds(-secondsAgo), Now));
    }
}
