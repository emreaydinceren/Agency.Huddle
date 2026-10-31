using Microsoft.Extensions.Logging;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins what Instructions and Memory §4.2 changes in <see cref="MemoryIndex"/>: a frontmatter <c>description</c> is the
/// index line, and the first-line fallback no longer reads <c>---</c> as the summary of a file that starts with frontmatter.
/// </summary>
public sealed class MemoryIndexDescriptionTests
{
    /// <summary>Writes one memory file in a fresh folder and builds the index over it.</summary>
    /// <param name="dataDir">The temporary data folder.</param>
    /// <param name="content">The file's whole text.</param>
    /// <param name="warnings">Warns about malformed frontmatter, or null.</param>
    private static MemoryEntry IndexOne(TempDataDir dataDir, string content, MemoryWarnings? warnings = null)
    {
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "topic.md"), content);

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100, warnings);

        return Assert.Single(entries);
    }

    /// <summary>A frontmatter <c>description</c> is the summary, ahead of the body's first line.</summary>
    [Fact]
    public void Build_Description_IsPreferredOverTheFirstLine()
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, "---\ndescription: Dentist visits and insurance. Read before booking.\n---\n\n# Dentist\n\n- Dr Lee.");

        Assert.Equal("Dentist visits and insurance. Read before booking.", entry.Summary);
    }

    /// <summary>A quoted description, which is how a description with a colon is written, is read without its quotes.</summary>
    [Theory]
    [InlineData("description: 'Teachers: who to email. Read for school mail.'", "Teachers: who to email. Read for school mail.")]
    [InlineData("description: \"Mum's phone: 555. Read for calls.\"", "Mum's phone: 555. Read for calls.")]
    public void Build_QuotedDescription_IsUnquoted(string line, string expected)
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, $"---\n{line}\n---\n\nBody.");

        Assert.Equal(expected, entry.Summary);
    }

    /// <summary>A description past 200 characters is cut at 200 with an ellipsis, as a first line is.</summary>
    [Fact]
    public void Build_LongDescription_CutAt200WithEllipsis()
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, $"---\ndescription: {new string('d', 250)}\n---\n\nBody.");

        Assert.Equal(new string('d', 200) + "…", entry.Summary);
    }

    /// <summary>A file that starts with frontmatter but has no description is indexed by the first body line, never by <c>---</c> (today's bug).</summary>
    [Fact]
    public void Build_FrontmatterWithoutDescription_FallsBackToTheFirstBodyLine()
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, "---\ntitle: School\ntags: [a, b]\n---\n\n# Ms Patel teaches Year 4.\n\nMore.");

        Assert.Equal("Ms Patel teaches Year 4.", entry.Summary);
    }

    /// <summary>A file with no frontmatter keeps today's first-line summary.</summary>
    [Fact]
    public void Build_NoFrontmatter_StillUsesTheFirstLine()
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, "The Human prefers C#.\n\nMore.");

        Assert.Equal("The Human prefers C#.", entry.Summary);
    }

    /// <summary>An unusable description (a list, or blank) falls back to the first body line and logs one Warning per file and content, however often the index is built.</summary>
    /// <param name="descriptionLine">The description line as written.</param>
    [Theory]
    [InlineData("description: [a, b]")]
    [InlineData("description:")]
    public void Build_MalformedDescription_FallsBackAndWarnsOnce(string descriptionLine)
    {
        using TempDataDir dataDir = new();
        RecordingLogger<MemoryWarnings> logger = new();
        MemoryWarnings warnings = new(logger);
        string content = $"---\n{descriptionLine}\n---\n\nFirst body line.";

        MemoryEntry first = IndexOne(dataDir, content, warnings);
        _ = MemoryIndex.Build(Path.Combine(dataDir.Path, "memory"), 100, warnings);

        Assert.Equal("First body line.", first.Summary);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>A frontmatter block that never closes is not frontmatter: the fallback skips the stray <c>---</c> line, warns, and uses the next line.</summary>
    [Fact]
    public void Build_UnclosedFrontmatter_SkipsTheMarkerAndWarns()
    {
        using TempDataDir dataDir = new();
        RecordingLogger<MemoryWarnings> logger = new();
        MemoryWarnings warnings = new(logger);

        MemoryEntry entry = IndexOne(dataDir, "---\ndescription: never closed\n\nBody.", warnings);

        Assert.Equal("description: never closed", entry.Summary);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>Only the start of a file is read: a description is found however long the body is, and a first body line past the scan bound is not looked for.</summary>
    [Fact]
    public void Build_ReadsOnlyTheStartOfAFile()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "described.md"), "---\ndescription: Found at the top of a very large file.\n---\n\n" + string.Concat(Enumerable.Repeat("A long line of body text.\n", 50_000)));
        File.WriteAllText(Path.Combine(memoryDir, "late.md"), string.Concat(Enumerable.Repeat("\n", 500)) + "A first line far past the bound.\n");

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        Assert.Equal(["Found at the top of a very large file.", "late"], entries.Select(static entry => entry.Summary).ToArray());
    }

    /// <summary>A long frontmatter block still ends where it ends, and the first body line after it is the summary.</summary>
    [Fact]
    public void Build_LongFrontmatterBlock_StillFindsTheFirstBodyLine()
    {
        using TempDataDir dataDir = new();
        string block = string.Concat(Enumerable.Range(0, 120).Select(static i => $"key{i}: value{i}\n"));

        MemoryEntry entry = IndexOne(dataDir, $"---\n{block}---\n\n# The first body line.\n");

        Assert.Equal("The first body line.", entry.Summary);
    }

    /// <summary>The warn-once memory is bounded: after it fills it is cleared, so a file may be warned about again but the set never grows without limit.</summary>
    [Fact]
    public void Warnings_AreRememberedPerFileAndContent_WithABound()
    {
        RecordingLogger<MemoryWarnings> logger = new();
        MemoryWarnings warnings = new(logger);

        warnings.Malformed("a.md", "x", "bad");
        warnings.Malformed("a.md", "x", "bad");
        warnings.Malformed("a.md", "y", "bad");
        for (int i = 0; i < MemoryWarnings.MaxRemembered; i++)
        {
            warnings.Malformed($"f{i}.md", "x", "bad");
        }

        int before = logger.AtExactly(LogLevel.Warning).Count;
        warnings.Malformed("a.md", "x", "bad");

        Assert.Equal(2 + MemoryWarnings.MaxRemembered, before);
        Assert.Equal(before + 1, logger.AtExactly(LogLevel.Warning).Count);
    }
    /// <summary>An entry knows its file name, which the index line shows.</summary>
    [Fact]
    public void Entry_FileName_IsTheNameWithItsExtension()
    {
        using TempDataDir dataDir = new();

        MemoryEntry entry = IndexOne(dataDir, "A fact.");

        Assert.Equal("topic.md", entry.FileName);
    }
}
