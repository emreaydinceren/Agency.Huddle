using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>Pins <see cref="MemoryEntry.FileName"/>, the name an index line shows, on every platform.</summary>
public sealed class MemoryEntryTests
{
    /// <summary>A Windows-style path and a POSIX path both give the bare file name, whatever platform runs the test.</summary>
    /// <param name="fullPath">The entry's full path.</param>
    [Theory]
    [InlineData(@"E:\Huddle\App_Data\work\Nova\memory\code-language.md")]
    [InlineData("/home/huddle/App_Data/work/Nova/memory/code-language.md")]
    [InlineData("code-language.md")]
    public void FileName_IsTheLastSegmentOfEitherKindOfPath(string fullPath)
    {
        Assert.Equal("code-language.md", new MemoryEntry("A summary", fullPath).FileName);
    }
}
