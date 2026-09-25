using System.Globalization;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for LibrarySize.Format human-readable size strings (Spec §6.14).</summary>
public sealed class LibrarySizeTests
{
    /// <summary>Format converts bytes to human-readable text: B, KB, or MB.</summary>
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1025, "2 KB")]
    [InlineData(3000, "3 KB")]
    [InlineData(1048575, "1024 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1100000, "1 MB")]
    [InlineData(1153434, "1.1 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(2097152, "2 MB")]
    public void Format_ReturnsExpectedText(long bytes, string expected)
    {
        string result = LibrarySize.Format(bytes);

        Assert.Equal(expected, result);
    }

    /// <summary>Format throws ArgumentOutOfRangeException for negative bytes.</summary>
    [Fact]
    public void Format_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LibrarySize.Format(-1));
    }

    /// <summary>Format handles long.MaxValue without throwing.</summary>
    [Fact]
    public void Format_LongMaxValue_DoesNotThrow()
    {
        string result = LibrarySize.Format(long.MaxValue);

        Assert.EndsWith(" MB", result, StringComparison.Ordinal);
    }

    /// <summary>Format is culture-invariant; de-DE uses the same format as invariant.</summary>
    [Fact]
    public void Format_IsCultureInvariant()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            string result = LibrarySize.Format(1572864);

            Assert.Equal("1.5 MB", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
