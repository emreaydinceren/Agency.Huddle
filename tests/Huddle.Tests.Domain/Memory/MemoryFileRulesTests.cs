using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Memory;

/// <summary>Pins <see cref="MemoryFileRules.IsValidFileName"/> on the names a memory folder reserves.</summary>
public sealed class MemoryFileRulesTests
{
    /// <summary>
    /// <c>MEMORY.md</c> is reserved, ignoring case: the index is built from each file's description, so a hand-kept index
    /// file of that name is refused rather than written as a topic.
    /// </summary>
    /// <param name="file">The name as an Agent might write it.</param>
    [Theory]
    [InlineData("MEMORY.md")]
    [InlineData("memory.md")]
    [InlineData("Memory.MD")]
    public void IsValidFileName_TheIndexFileName_IsRefused(string file)
    {
        Assert.False(MemoryFileRules.IsValidFileName(file));
    }

    /// <summary>Only the exact reserved name is refused: a topic whose name merely starts with "memory" is accepted.</summary>
    [Fact]
    public void IsValidFileName_ANameThatOnlyStartsWithMemory_IsAccepted()
    {
        Assert.True(MemoryFileRules.IsValidFileName("memory-notes.md"));
    }

    /// <summary>The reserved index name is recognised ignoring case, and nothing else is.</summary>
    /// <param name="file">A file name.</param>
    /// <param name="expected">Whether it is the reserved index name.</param>
    [Theory]
    [InlineData("MEMORY.md", true)]
    [InlineData("memory.MD", true)]
    [InlineData("memory-notes.md", false)]
    [InlineData("MEMORY.md.bak", false)]
    [InlineData("dentist.md", false)]
    public void IsIndexFileName_MatchesOnlyTheReservedName(string file, bool expected)
    {
        Assert.Equal(expected, MemoryFileRules.IsIndexFileName(file));
    }
}
