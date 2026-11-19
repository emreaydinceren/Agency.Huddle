using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskFileFormat"/>'s optional <c>rank</c> key: a positive whole number, empty or absent meaning none.</summary>
public sealed class TaskFileFormatRankTests
{
    private static readonly TaskLocation DefaultLocation = new("Platform", null, false);

    /// <summary>A <c>rank: N</c> line parses to the number, and surrounding spaces are ignored.</summary>
    [Theory]
    [InlineData("rank: 3", 3)]
    [InlineData("rank:   12  ", 12)]
    [InlineData("rank: 1", 1)]
    [InlineData("rank: 2147483647", int.MaxValue)]
    public void TryParse_Rank_ReadsTheNumber(string line, int expected)
    {
        bool result = TaskFileFormat.TryParse(TextWith(line), "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Equal(expected, task.Rank);
        Assert.Empty(error);
        Assert.Empty(task.UnknownFields);
    }

    /// <summary>A value that is not a whole number of 1 or more rejects the file, naming rank and the value.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("x")]
    [InlineData("1.5")]
    [InlineData("+3")]
    [InlineData("2147483648")]
    public void TryParse_BadRank_Fails(string value)
    {
        bool result = TaskFileFormat.TryParse(TextWith($"rank: {value}"), "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.False(result);
        Assert.Null(task);
        Assert.Equal($"invalid rank: '{value}'", error);
    }

    /// <summary>An absent <c>rank</c> key and an empty one both mean no rank.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("rank:")]
    [InlineData("rank:   ")]
    public void TryParse_AbsentOrEmptyRank_IsNull(string? line)
    {
        bool result = TaskFileFormat.TryParse(TextWith(line), "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Null(task.Rank);
        Assert.Empty(error);
    }

    /// <summary>A Task with a rank composes <c>rank:</c> right after <c>priority</c>.</summary>
    [Fact]
    public void Compose_Rank_SitsRightAfterPriority()
    {
        TaskItem task = TestTasks.Make() with { Rank = 7 };

        string[] lines = TaskFileFormat.Compose(task).Split('\n');

        int priorityIndex = Array.FindIndex(lines, static line => line.StartsWith("priority:", StringComparison.Ordinal));
        Assert.True(priorityIndex >= 0);
        Assert.Equal("rank: 7", lines[priorityIndex + 1]);
    }

    /// <summary>A Task with no rank writes no <c>rank:</c> line.</summary>
    [Fact]
    public void Compose_NoRank_WritesNoRankLine()
    {
        string text = TaskFileFormat.Compose(TestTasks.Make());

        Assert.DoesNotContain("rank:", text, StringComparison.Ordinal);
    }

    /// <summary>Compose then TryParse returns the same rank, and the key is not reported as unknown.</summary>
    [Fact]
    public void Compose_ThenTryParse_RoundTripsRank()
    {
        string text = TaskFileFormat.Compose(TestTasks.Make() with { Rank = 40 });

        bool result = TaskFileFormat.TryParse(text, "p.md", DefaultLocation, out TaskItem? task, out string error);

        Assert.True(result);
        Assert.NotNull(task);
        Assert.Equal(40, task.Rank);
        Assert.Empty(error);
        Assert.Empty(task.UnknownFields);
    }

    /// <summary>Rewriting the head of a stored file keeps an existing rank and writes a changed one.</summary>
    [Fact]
    public void ReplaceHead_ChangedRank_IsWritten()
    {
        TaskItem task = TestTasks.Make() with { Rank = 2 };
        string current = TaskFileFormat.Compose(task);

        string replaced = TaskFileFormat.ReplaceHead(current, task with { Rank = 9 });

        // contains-ok: the haystack is the composed Task file's front matter, and the assert pins the one `rank:` line, not rendered markup.
        Assert.Contains("rank: 9", replaced, StringComparison.Ordinal);
        Assert.DoesNotContain("rank: 2", replaced, StringComparison.Ordinal);
    }

    /// <summary>Builds a minimal valid Task file with <paramref name="rankLine"/> after the priority line, or none when null.</summary>
    /// <param name="rankLine">The line to insert, or <see langword="null"/> for none.</param>
    private static string TextWith(string? rankLine)
    {
        List<string> lines =
        [
            "---",
            "id: PLAT-0042",
            "title: 'A Task'",
            "status: Backlog",
            "priority: Medium",
            "creator: Emre",
        ];
        if (rankLine is not null)
        {
            lines.Add(rankLine);
        }

        lines.AddRange(["---", "Body.", "", "## Change log", "- 2026-09-24T11:20:00Z | Emre | created"]);
        return string.Join('\n', lines) + "\n";
    }
}
