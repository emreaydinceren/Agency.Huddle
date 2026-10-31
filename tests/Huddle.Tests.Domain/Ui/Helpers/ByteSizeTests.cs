namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Components.Shared.ByteSize.Format"/>: bytes below 1 KB read as B, below 1 MB as whole KB rounded half
/// away from zero, and above that as MB with one decimal, in invariant culture. A 5 MB file reads
/// "5.0 MB", not "5120 KB" (UX-R25).
/// </summary>
public sealed class ByteSizeTests
{
    /// <summary>Each size lands in the unit the Composer and the Library both use.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="expected">The text the helper must return.</param>
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "2 KB")]
    [InlineData(1048576L, "1.0 MB")]
    [InlineData(1572864L, "1.5 MB")]
    [InlineData(5242880L, "5.0 MB")]
    public void Format_SizeReadsInTheUnitItFallsIn(long bytes, string expected)
    {
        Assert.Equal(expected, Agency.Huddle.App.Components.Shared.ByteSize.Format(bytes));
    }
}
