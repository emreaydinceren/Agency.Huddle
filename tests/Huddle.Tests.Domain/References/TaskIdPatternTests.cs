using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins <see cref="TaskIdPattern.Candidate"/>: the one pattern that finds a stand-alone Task id in text.</summary>
public sealed class TaskIdPatternTests
{
    /// <summary>The shared pattern is the renderer's pattern, character for character (spec §13.13.2, D-32).</summary>
    [Fact]
    public void Candidate_MatchesTheRendererPattern()
    {
        string pattern = TaskIdPattern.Candidate().ToString();

        Assert.Equal(@"(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])", pattern);
    }

    /// <summary>A lower-case prefix is not a Task id, so it is never a candidate.</summary>
    [Fact]
    public void Candidate_RejectsLowerCasePrefix()
    {
        bool found = TaskIdPattern.Candidate().IsMatch("see grow-0002 now");

        Assert.False(found);
    }

    /// <summary>An id glued to a word character or a dash on either side is not a stand-alone id.</summary>
    [Theory]
    [InlineData("aGROW-0002")]
    [InlineData("GROW-0002b")]
    [InlineData("GROW-0002-")]
    [InlineData("_GROW-0002")]
    public void Candidate_RejectsAdjacentWordCharacters(string text)
    {
        bool found = TaskIdPattern.Candidate().IsMatch(text);

        Assert.False(found);
    }
}
