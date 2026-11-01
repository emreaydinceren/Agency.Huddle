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

    /// <summary>A leading frontmatter block and the blank lines after it are removed from a body; a body without one keeps its text, minus a byte-order mark.</summary>
    /// <param name="text">The body as given.</param>
    /// <param name="expected">The body to write.</param>
    [Theory]
    [InlineData("---\ndescription: Echoed\n---\n\nThe body.\n", "The body.\n")]
    [InlineData("\uFEFF---\ndescription: Echoed\n---\nThe body.\n", "The body.\n")]
    [InlineData("The body.\n---\nnot frontmatter\n---\n", "The body.\n---\nnot frontmatter\n---\n")]
    [InlineData("{\"endpoint\": \"/v2/orders\", \"retries\": 3}\n\nCall it twice at most.\n", "{\"endpoint\": \"/v2/orders\", \"retries\": 3}\n\nCall it twice at most.\n")]
    [InlineData("---\ndescription: Echoed\n---\n", "")]
    [InlineData("---\nretries: 3\n---\nThe client's default.\n", "---\nretries: 3\n---\nThe client's default.\n")]
    [InlineData("\uFEFFThe body.", "The body.")]
    public void SplitEcho_RemovesOnlyALeadingBlock(string text, string expected)
    {
        Assert.Equal(expected, MemoryFileRules.SplitEcho(text, out _));
    }

    /// <summary>The description is read from the frontmatter when it is a non-blank single value; a body line is never a description.</summary>
    /// <param name="text">A file's text.</param>
    /// <param name="expected">The description, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("---\ndescription: Consent rules for interviews\n---\n\nBody.\n", "Consent rules for interviews")]
    [InlineData("---\nowner: Ana\n---\n\nBody.\n", null)]
    [InlineData("---\ndescription:\n---\n\nBody.\n", null)]
    [InlineData("description: Not frontmatter\n", null)]
    public void DescriptionOf_ReadsTheFrontmatterKeyOnly(string text, string? expected)
    {
        Assert.Equal(expected, MemoryFileRules.DescriptionOf(text));
    }

    /// <summary>The kept keys are every line but the description and its continuation lines; the JSON form and a file without frontmatter give none.</summary>
    /// <param name="text">A file's text.</param>
    /// <param name="expected">The kept lines, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("---\ndescription: D\nowner: Ana\n---\n\nBody.\n", "owner: Ana\n")]
    [InlineData("---\nDescription: |\n  two\n  lines\nstatus: draft\n---\n", "status: draft\n")]
    [InlineData("---\ndescription: D\n---\n", null)]
    [InlineData("{\"description\": \"D\", \"owner\": \"Ana\"}\n\nBody.\n", null)]
    [InlineData("No frontmatter.\n", null)]
    public void OtherKeys_KeepsEveryKeyButTheDescription(string text, string? expected)
    {
        Assert.Equal(expected, MemoryFileRules.OtherKeys(text));
    }

    /// <summary>An echoed header gives up its keys other than the description; a text without one gives none.</summary>
    /// <param name="text">The text as given.</param>
    /// <param name="keys">The echoed keys, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("---\ndescription: D\nowner: Bo\n---\n\nBody.\n", "owner: Bo\n")]
    [InlineData("---\ndescription: D\n---\n\nBody.\n", null)]
    [InlineData("Body.\n", null)]
    public void SplitEcho_ReturnsTheEchoedKeys(string text, string? keys)
    {
        _ = MemoryFileRules.SplitEcho(text, out string? echoed);

        Assert.Equal(keys, echoed);
    }

    /// <summary>Echoed keys replace the same keys, ignoring case, with their continuation lines, and new ones follow the file's.</summary>
    /// <param name="kept">The file's keys.</param>
    /// <param name="echoed">The echoed keys.</param>
    /// <param name="expected">The merged keys.</param>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("owner: Ana\n", null, "owner: Ana\n")]
    [InlineData(null, "owner: Bo\n", "owner: Bo\n")]
    [InlineData("owner: Ana\ntags:\n  - legal\n", "Owner: Bo\ntags:\n  - hr\nstatus: x\n", "Owner: Bo\ntags:\n  - hr\nstatus: x\n")]
    [InlineData("owner: Ana\ntags:\n  - legal\n", "status: x\n", "owner: Ana\ntags:\n  - legal\nstatus: x\n")]
    public void MergeKeys_EchoedReplacesThenAppends(string? kept, string? echoed, string? expected)
    {
        Assert.Equal(expected, MemoryFileRules.MergeKeys(kept, echoed));
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
