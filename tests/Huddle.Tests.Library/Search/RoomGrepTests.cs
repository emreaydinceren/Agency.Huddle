using System.Text.RegularExpressions;
using Agency.Huddle.App.Search;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins the pure helpers of <see cref="RoomGrep"/> and <see cref="RoomMatcher"/>: windows, filters, times, lines and the regex timeout.</summary>
public sealed class RoomGrepTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Two matches whose windows overlap become one window, so a shared neighbour is listed once.</summary>
    [Fact]
    public void Windows_OverlappingMatches_AreMerged()
    {
        IReadOnlyList<(int Start, int End)> windows = RoomGrep.Windows([1, 3], 1, 10);

        Assert.Equal([(0, 4)], windows);
    }

    /// <summary>Windows that only touch are merged, and windows with a gap stay apart; the ends are clamped to the Room.</summary>
    [Fact]
    public void Windows_TouchingMerge_GapsStayApart_AndEndsAreClamped()
    {
        IReadOnlyList<(int Start, int End)> windows = RoomGrep.Windows([0, 3, 9], 1, 10);

        Assert.Equal([(0, 4), (8, 9)], windows);
    }

    /// <summary>Context 0 shows only the matches themselves.</summary>
    [Fact]
    public void Windows_ContextZero_IsTheMatchesAlone()
    {
        IReadOnlyList<(int Start, int End)> windows = RoomGrep.Windows([2, 5], 0, 10);

        Assert.Equal([(2, 2), (5, 5)], windows);
    }

    /// <summary>A date <c>until</c> runs through the end of that day, a date <c>since</c> starts it, and a bad value is refused.</summary>
    [Fact]
    public void TryParseTime_DateAndIso_AreUtc()
    {
        bool since = RoomGrep.TryParseTime("2026-10-28", false, out DateTimeOffset start);
        bool until = RoomGrep.TryParseTime("2026-10-28", true, out DateTimeOffset end);
        bool iso = RoomGrep.TryParseTime("2026-10-28T09:30:00+02:00", false, out DateTimeOffset offset);
        bool bad = RoomGrep.TryParseTime("not a time", false, out _);

        Assert.True(since);
        Assert.True(until);
        Assert.True(iso);
        Assert.False(bad);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero), end);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 7, 30, 0, TimeSpan.Zero), offset.ToUniversalTime());
    }

    /// <summary>Every matching Message is listed, not only the newest, and the sender filter is matched on the displayed name.</summary>
    [Fact]
    public void MatchIndexes_ListsEveryMatch_AndHonoursSenderAndTime()
    {
        List<ChatMessage> messages =
        [
            new("a", T0, "kai-id", "Kai", "needle one"),
            new("b", T0.AddHours(1), "nova-id", "Nova", "needle two"),
            new("c", T0.AddHours(2), "kai-id", "Kai", "needle three"),
            new("d", T0.AddHours(3), "kai-id", "Kai", "other"),
        ];

        List<int> all = RoomGrep.MatchIndexes(messages, new RoomGrepOptions(RoomMatcher.ForPhrase("NEEDLE"), 10), null);
        List<int> kai = RoomGrep.MatchIndexes(messages, new RoomGrepOptions(RoomMatcher.ForPhrase("needle"), 10) { Sender = "kai", Since = T0.AddHours(1) }, null);

        Assert.Equal([0, 1, 2], all);
        Assert.Equal([2], kai);
    }

    /// <summary>A line collapses line breaks, marks a match, and is cut to 300 characters with an ellipsis.</summary>
    [Fact]
    public void MessageLine_CollapsesBreaks_AndCutsLongText()
    {
        ChatMessage message = new("a", T0, "kai-id", "Kai", "first\nsecond " + new string('x', 400));

        string line = RoomGrep.MessageLine(message, null, "> ");

        Assert.StartsWith("> 2026-10-28 09:00 Kai: first second xxx", line, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.EndsWith("…", line, StringComparison.Ordinal); // contains-ok: one line or fragment of plain-text tool output
        Assert.DoesNotContain('\n', line);
        Assert.Equal("> 2026-10-28 09:00 Kai: ".Length + 301, line.Length);
    }

    /// <summary>A regular expression that backtracks without end throws the timeout exception rather than running on.</summary>
    [Fact]
    public void Regex_Catastrophic_TimesOut()
    {
        bool built = RoomMatcher.TryForRegex("(a+)+$", out RoomMatcher? matcher, out _);

        Assert.True(built);
        Assert.NotNull(matcher);
        Assert.Throws<RegexMatchTimeoutException>(() => matcher.IsMatch(new string('a', 40000) + "!"));
    }

    /// <summary>An invalid pattern is reported, and a regex snippet is a window around the first match.</summary>
    [Fact]
    public void Regex_InvalidIsReported_AndSnippetSurroundsTheMatch()
    {
        bool invalid = RoomMatcher.TryForRegex("(open", out RoomMatcher? none, out string error);
        bool valid = RoomMatcher.TryForRegex("wait\\s+for", out RoomMatcher? matcher, out _);

        Assert.False(invalid);
        Assert.Null(none);
        Assert.NotEmpty(error);
        Assert.True(valid);
        Assert.NotNull(matcher);
        Assert.Equal("please wait   for me", matcher.Snippet("please wait   for me", 120));
    }
}
 // contains-ok: one line or fragment of plain-text tool output