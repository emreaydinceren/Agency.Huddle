using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="SprintId"/> (Sprints spec §7.1).</summary>
public sealed class SprintIdTests
{
    /// <summary>Valid ids parse case-insensitively, trim whitespace and drop leading zeros.</summary>
    [Theory]
    [InlineData("S2", 2)]
    [InlineData("s2", 2)]
    [InlineData("s02", 2)]
    [InlineData(" S12 ", 12)]
    [InlineData("S999999999", 999999999)]
    public void TryParse_Valid_Parses(string input, int expectedNumber)
    {
        bool parsed = SprintId.TryParse(input, out SprintId id);

        Assert.True(parsed);
        Assert.Equal(expectedNumber, id.Number);
    }

    /// <summary>Anything outside the grammar is rejected, including zero, a task id and ten digits.</summary>
    [Theory]
    [InlineData("S0")]
    [InlineData("S000")]
    [InlineData("S")]
    [InlineData("PLAT-1")]
    [InlineData("S1234567890")]
    [InlineData("S 1")]
    [InlineData("S-1")]
    [InlineData("2")]
    [InlineData("banana")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        bool parsed = SprintId.TryParse(input, out SprintId id);

        Assert.False(parsed);
        Assert.Equal(default(SprintId), id);
    }

    /// <summary>ToString prints an upper-case S and the number with no padding.</summary>
    [Theory]
    [InlineData(1, "S1")]
    [InlineData(2, "S2")]
    [InlineData(42, "S42")]
    [InlineData(12345, "S12345")]
    public void ToString_NoPadding(int number, string expected)
    {
        SprintId id = new(number);

        Assert.Equal(expected, id.ToString());
    }

    /// <summary>Parsing the printed form gives back the same id, and a padded form prints unpadded.</summary>
    [Fact]
    public void ParseThenPrint_RoundTrips()
    {
        bool parsed = SprintId.TryParse("s02", out SprintId id);
        bool reparsed = SprintId.TryParse(id.ToString(), out SprintId again);

        Assert.True(parsed);
        Assert.True(reparsed);
        Assert.Equal("S2", id.ToString());
        Assert.Equal(id, again);
    }

    /// <summary>Two ids with the same number are equal and work as a dictionary key without a comparer.</summary>
    [Fact]
    public void Equality_IsByValue_AndUsableAsDictionaryKey()
    {
        _ = SprintId.TryParse("s3", out SprintId lower);
        _ = SprintId.TryParse("S03", out SprintId padded);
        Dictionary<SprintId, string> names = new() { [lower] = "Three" };

        Assert.Equal(lower, padded);
        Assert.Equal(lower.GetHashCode(), padded.GetHashCode());
        Assert.NotEqual(lower, new SprintId(4));
        Assert.Equal("Three", names[padded]);
    }

    /// <summary>A Sprint number below one cannot be constructed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NumberBelowOne_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SprintId(number));
    }

    /// <summary>A Sprint id can never be mistaken for a Task id, and the reverse.</summary>
    [Fact]
    public void SprintId_IsNotATaskId()
    {
        Assert.False(TaskId.TryParse("S2", out _));
        Assert.False(SprintId.TryParse("PLAT-0002", out _));
    }
}
