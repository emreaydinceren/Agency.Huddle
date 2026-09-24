using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskId"/>.</summary>
public sealed class TaskIdTests
{
    /// <summary>Valid TaskIds parse correctly with case-insensitive prefix and number parsing.</summary>
    [Theory]
    [InlineData("PLAT-0042", "PLAT", 42)]
    [InlineData("plat-42", "PLAT", 42)]
    [InlineData(" A-1 ", "A", 1)]
    [InlineData("ABCDEFGH-123456789", "ABCDEFGH", 123456789)]
    public void TryParse_Valid_Parses(string input, string expectedPrefix, int expectedNumber)
    {
        bool parsed = TaskId.TryParse(input, out TaskId id);

        Assert.True(parsed);
        Assert.Equal(expectedPrefix, id.Prefix);
        Assert.Equal(expectedNumber, id.Number);
    }

    /// <summary>Invalid TaskIds return false.</summary>
    [Theory]
    [InlineData("PLAT")]
    [InlineData("-42")]
    [InlineData("1PLAT-4")]
    [InlineData("ABCDEFGHI-1")]  // 9-character prefix
    [InlineData("PLAT-")]
    [InlineData("PLAT-1234567890")]  // 10 digits
    [InlineData("PL AT-1")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1")]  // Just a number
    [InlineData("0")]  // Just zero
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        bool parsed = TaskId.TryParse(input, out _);
        Assert.False(parsed);
    }

    /// <summary>ToString pads the number to at least 4 digits.</summary>
    [Theory]
    [InlineData(42, "PLAT-0042")]
    [InlineData(12345, "PLAT-12345")]
    public void ToString_PadsToFourDigits(int number, string expected)
    {
        TaskId id = new("PLAT", number);
        Assert.Equal(expected, id.ToString());
    }

    /// <summary>Equality is based on the normalized prefix and number, so different cases are equal.</summary>
    [Fact]
    public void Equality_IsCaseInsensitiveViaParse()
    {
        bool parsed1 = TaskId.TryParse("plat-42", out TaskId id1);
        bool parsed2 = TaskId.TryParse("PLAT-0042", out TaskId id2);

        Assert.True(parsed1);
        Assert.True(parsed2);
        Assert.Equal(id1, id2);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    /// <summary>Constructor with lowercase prefix uppercases it via property initializer.</summary>
    [Fact]
    public void Constructor_LowerCasePrefix_Uppercases()
    {
        TaskId id = new("plat", 42);
        Assert.Equal("PLAT", id.Prefix);
        Assert.Equal(42, id.Number);
    }
}
