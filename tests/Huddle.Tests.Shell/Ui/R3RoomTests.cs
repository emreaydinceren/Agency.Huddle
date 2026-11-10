using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

using System.Globalization;
using Agency.Huddle.App.Components.Shared;

/// <summary>
/// Round 3 (audit) guards for the Room: the day separator and the time under it (B5a), the header as a
/// <c>PageHeader</c> (B1), the reference chip's Edge (B5d) and the Agent cards' column (C8). The browser
/// check is the orchestrator's; these pin the source that carries each property.
/// </summary>
public sealed class R3RoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A day older than yesterday reads weekday, month and day, with a comma, in every culture.</summary>
    [Fact]
    public void DayLabel_OlderDay_ReadsWeekdayCommaMonthDay()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            DateTimeOffset monday = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

            Assert.Equal("Mon, Oct 5", MessageDisplay.DayLabel(monday, monday.AddDays(3)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>A day in another year carries the year.</summary>
    [Fact]
    public void DayLabel_OtherYear_CarriesTheYear()
    {
        DateTimeOffset earlier = new(2025, 10, 6, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("Mon, Oct 6, 2025", MessageDisplay.DayLabel(earlier, R3RoomTests.Now));
    }

    /// <summary>Under a day separator a message shows the time only, never a weekday, even for an older day.</summary>
    [Theory]
    [InlineData(2026, 10, 14, 9, 30, "09:30")]
    [InlineData(2026, 10, 5, 23, 59, "23:59")]
    [InlineData(2025, 1, 2, 2, 7, "02:07")]
    public void ClockTime_IsTheTimeOnly(int year, int month, int day, int hour, int minute, string expected)
    {
        DateTimeOffset at = new(year, month, day, hour, minute, 0, R3RoomTests.Now.Offset);

        Assert.Equal(expected, MessageDisplay.ClockTime(at, R3RoomTests.Now));
    }

    /// <summary>The Room header is a <c>PageHeader</c> with a Chrome line, and keeps the inline rename.</summary>
    [Fact]
    public void ChatPage_Header_IsAPageHeaderWithChromeAndKeepsTheRename()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Chat.razor"));

        Assert.Contains("<PageHeader Title=\"@this.room.Name\" Chrome=\"true\"", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("TitleEditor=", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("title-inline-edit", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("<PageTitle>", markup, StringComparison.Ordinal);
    }

    /// <summary>A reference chip is a tag chip, which carries an Edge so it shows on Huddle Dark.</summary>
    [Fact]
    public void ReferenceChip_IsATagChipWithAnEdge()
    {
        string chip = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "References", "MessageReferences.razor"));
        string css = CssText.AppCss(CssNewlines.AsIs);
        int rule = css.IndexOf("\n.mud-chip.tag-chip {", StringComparison.Ordinal);

        Assert.Contains("tag-chip", chip, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.True(rule >= 0, "app.css has no .mud-chip.tag-chip rule.");
        Assert.Contains("border: 1px solid var(--ink-line);", css[rule..css.IndexOf('}', rule)], StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>Every Agent card sits in the Transcript's 760px column with an Edge border.</summary>
    [Fact]
    public void AgentCard_SitsInTheTranscriptColumnWithAnEdge()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int rule = css.IndexOf("\n.mud-paper.agent-card {", StringComparison.Ordinal);

        Assert.True(rule >= 0, "app.css has no .mud-paper.agent-card rule.");
        string body = css[rule..css.IndexOf('}', rule)];
        Assert.Contains("max-width: 760px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("border-color: var(--ink-line);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }
}
