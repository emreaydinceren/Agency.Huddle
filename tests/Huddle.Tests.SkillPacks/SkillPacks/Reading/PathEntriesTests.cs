using System.Text.Json;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.Tests.SkillPacks.Reading;

/// <summary>Pins §8.2: a computer's own entry beats the plain one, a drive letter is never a computer name, the JSON
/// <c>"*"</c> key, and the R010 and R013 shapes.</summary>
public sealed class PathEntriesTests
{
    /// <summary>The computer's own entry wins over the plain entry, matched ignoring case.</summary>
    [Fact]
    public void Resolve_ComputerEntry_BeatsPlainEntry()
    {
        PathEntries entries = PathEntries.Parse("plain.json\nDESK-01: own.json");

        Assert.Equal("own.json", entries.Resolve("desk-01"));
        Assert.Equal("plain.json", entries.Resolve("LAPTOP"));
    }

    /// <summary>With only another computer's entry there is no path here, and HasEntryFor is false (R009).</summary>
    [Fact]
    public void Resolve_OtherComputerOnly_ReturnsNull()
    {
        PathEntries entries = PathEntries.Parse("DESK-01: own.json");

        Assert.Null(entries.Resolve("LAPTOP"));
        Assert.False(entries.HasEntryFor("LAPTOP"));
    }

    /// <summary><c>E:\x</c> is a plain path: a drive letter is followed by a backslash, never a space.</summary>
    [Fact]
    public void Resolve_DriveLetterPath_IsNotAComputerEntry()
    {
        PathEntries entries = PathEntries.Parse(@"E:\Clones\x\a.skillcatalog.json");

        PathEntry entry = Assert.Single(entries.Entries);
        Assert.Null(entry.Computer);
        Assert.Equal(@"E:\Clones\x\a.skillcatalog.json", entries.Resolve("ANY"));
    }

    /// <summary>A one-letter name followed by ": " is not a computer entry: it is a plain entry (whose path will not exist, R003).</summary>
    [Fact]
    public void Parse_OneLetterComputerLine_IsPlainEntryAndR003()
    {
        PathEntries entries = PathEntries.Parse("E: some path.json");

        PathEntry entry = Assert.Single(entries.Entries);
        Assert.Null(entry.Computer);
        Assert.Equal("E: some path.json", entry.Path);
        Assert.Matches(@"\A[A-Za-z0-9]: \S", entry.Path);
    }

    /// <summary>The JSON <c>"*"</c> key is the plain entry; other keys are computers.</summary>
    [Fact]
    public void FromJson_ObjectWithStar_IsPlainEntry()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "DESK-01": "a", "*": "b" }""");

        PathEntries? entries = PathEntries.FromJson(document.RootElement);

        Assert.NotNull(entries);
        Assert.Equal("a", entries.Resolve("DESK-01"));
        Assert.Equal("b", entries.Resolve("OTHER"));
        Assert.Contains(new PathEntry(null, "b"), entries.Entries);
    }

    /// <summary>A one-letter JSON key is still a computer name and resolves for that computer.</summary>
    [Fact]
    public void FromJson_OneLetterComputerKey_Resolves()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "E": "x" }""");

        PathEntries? entries = PathEntries.FromJson(document.RootElement);

        Assert.NotNull(entries);
        Assert.Equal("x", entries.Resolve("E"));
        Assert.Null(entries.Resolve("F"));
    }

    /// <summary>An empty string is an entry with no path: HasEntryFor is true, Resolve is null (Not downloaded, no R009).</summary>
    [Fact]
    public void FromJson_EmptyString_HasEntryButNoPath()
    {
        using JsonDocument document = JsonDocument.Parse("\"\"");

        PathEntries? entries = PathEntries.FromJson(document.RootElement);

        Assert.NotNull(entries);
        Assert.True(entries.HasEntryFor("PC"));
        Assert.Null(entries.Resolve("PC"));
    }

    /// <summary>Any JSON type other than a string, object or null is not a path.</summary>
    [Fact]
    public void FromJson_Number_IsNull()
    {
        using JsonDocument document = JsonDocument.Parse("12");

        Assert.Null(PathEntries.FromJson(document.RootElement));
    }

    /// <summary>The same computer named twice, ignoring case, is R010.</summary>
    [Fact]
    public void HasDuplicateComputer_R010()
    {
        Assert.True(PathEntries.Parse("PC-1: a\npc-1: b").HasDuplicateComputer);
        Assert.False(PathEntries.Parse("PC-1: a\nPC-2: b").HasDuplicateComputer);
    }

    /// <summary>Two plain entries are R013.</summary>
    [Fact]
    public void HasTwoPlainEntries_R013()
    {
        Assert.True(PathEntries.Parse("a\nb").HasTwoPlainEntries);
        Assert.False(PathEntries.Parse("a\nPC-1: b").HasTwoPlainEntries);
    }

    /// <summary>ResolveFull accepts either separator and resolves relative paths against the base folder.</summary>
    [Fact]
    public void ResolveFull_AcceptsBothSeparators()
    {
        string root = InMemoryFileReader.At("SS");

        string? forward = PathEntries.Parse("packs/a.skillpack.json").ResolveFull("PC", root);
        string? back = PathEntries.Parse(@"packs\a.skillpack.json").ResolveFull("PC", root);

        Assert.Equal(Path.Combine(root, "packs", "a.skillpack.json"), forward);
        Assert.Equal(forward, back);
    }
}
