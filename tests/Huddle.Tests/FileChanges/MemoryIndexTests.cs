using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>Pins <see cref="MemoryIndex.Build(string, int)"/>: FC §6.15's index rules.</summary>
public sealed class MemoryIndexTests
{
    /// <summary>The first non-blank line becomes the summary, with a leading <c>#</c> and spaces removed.</summary>
    [Fact]
    public void Build_FirstNonBlankLineIsSummary_HashesRemoved()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "language.md"), "# The Human prefers C#.\n\nSaid in Room 'Alpha'.");

        (IReadOnlyList<MemoryEntry> entries, int notListed) = MemoryIndex.Build(memoryDir, 100);

        MemoryEntry entry = Assert.Single(entries);
        Assert.Equal("The Human prefers C#.", entry.Summary);
        Assert.Equal(0, notListed);
    }

    /// <summary>A blank first line, or an empty file, falls back to the file name without the extension (E-15).</summary>
    [Fact]
    public void Build_BlankOrEmptyFile_FallsBackToFileName()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "blank-first-line.md"), "\n\n   \n");
        File.WriteAllText(Path.Combine(memoryDir, "empty.md"), string.Empty);

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Summary == "blank-first-line");
        Assert.Contains(entries, e => e.Summary == "empty");
    }

    /// <summary>A first line past 200 characters is cut at 200, with a trailing ellipsis (E-16).</summary>
    [Fact]
    public void Build_LongLine_CutAt200WithEllipsis()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        string longLine = new string('x', 250);
        File.WriteAllText(Path.Combine(memoryDir, "long.md"), longLine);

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        MemoryEntry entry = Assert.Single(entries);
        Assert.Equal(new string('x', 200) + "…", entry.Summary);
    }

    /// <summary>Entries are ordered by file name, <see cref="StringComparer.Ordinal"/>.</summary>
    [Fact]
    public void Build_OrderedByFileNameOrdinal()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "zebra.md"), "Z fact.");
        File.WriteAllText(Path.Combine(memoryDir, "alpha.md"), "A fact.");
        File.WriteAllText(Path.Combine(memoryDir, "mid.md"), "M fact.");

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        Assert.Equal(["A fact.", "M fact.", "Z fact."], entries.Select(e => e.Summary));
    }

    /// <summary>More than <c>maxEntries</c> files: the first by file name are listed, the rest counted in <see cref="MemoryIndex.Build(string, int)"/>'s <c>NotListed</c> (E-17).</summary>
    [Fact]
    public void Build_OverMax_CountsNotListed()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(memoryDir, $"fact{i}.md"), $"Fact {i}.");
        }

        (IReadOnlyList<MemoryEntry> entries, int notListed) = MemoryIndex.Build(memoryDir, 3);

        Assert.Equal(3, entries.Count);
        Assert.Equal(2, notListed);
    }

    /// <summary>Only <c>*.md</c> directly inside <c>memory/</c> is indexed: not other extensions, and not subfolders.</summary>
    [Fact]
    public void Build_NonMdAndSubfolders_NotIndexed()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "fact.md"), "A fact.");
        File.WriteAllText(Path.Combine(memoryDir, "notes.txt"), "Not indexed.");
        string sub = Path.Combine(memoryDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "nested.md"), "Not indexed either.");

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        MemoryEntry entry = Assert.Single(entries);
        Assert.Equal("A fact.", entry.Summary);
    }

    /// <summary>A missing <c>memory/</c> folder gives an empty index, not an error.</summary>
    [Fact]
    public void Build_MissingFolder_Empty()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "does-not-exist");

        (IReadOnlyList<MemoryEntry> entries, int notListed) = MemoryIndex.Build(memoryDir, 100);

        Assert.Empty(entries);
        Assert.Equal(0, notListed);
    }

    /// <summary>An entry's <see cref="MemoryEntry.FullPath"/> is the file's full path.</summary>
    [Fact]
    public void Build_FullPathIsTheFile()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "fact.md");
        File.WriteAllText(filePath, "A fact.");

        (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

        MemoryEntry entry = Assert.Single(entries);
        Assert.Equal(filePath, entry.FullPath);
    }

    /// <summary>A file that cannot be read falls back to its file name, per file, rather than failing the whole index (correction item 13).</summary>
    [Fact]
    public void Build_UnreadableFile_FallsBackToFileName()
    {
        using TempDataDir dataDir = new();
        string memoryDir = Path.Combine(dataDir.Path, "memory");
        Directory.CreateDirectory(memoryDir);
        string filePath = Path.Combine(memoryDir, "locked.md");
        File.WriteAllText(filePath, "A secret fact.");

        try
        {
            using FileStream lockStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

            (IReadOnlyList<MemoryEntry> entries, int _) = MemoryIndex.Build(memoryDir, 100);

            MemoryEntry entry = Assert.Single(entries);
            Assert.Equal("locked", entry.Summary);
        }
        catch (IOException)
        {
            Assert.Skip("Could not exclusively lock the file on this platform.");
        }
    }
}
