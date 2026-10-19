using System.Text;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.Tests.SkillPacks.Reading;

/// <summary>Pins §8.1: tables found by their level-2 heading, columns by header, cell flattening, row spans, and the
/// registry findings R001, R002, R011 and R012.</summary>
public sealed class RegistryReaderTests
{
    private static readonly string Dir = InMemoryFileReader.At("SS");

    private const string TwoTables = """
        # Skills registry

        We do not recommend editing this file by hand.

        ## Packs

        | Local path | Origin URL | Last updated | Notes |
        | --- | --- | --- | --- |
        | packs/a.skillpack.json | https://github.com/o/r | 2026-10-01 | First |
        | packs/b.skillpack.json | | | |

        ## Catalogs

        | Local path | Notes |
        | --- | --- |
        | catalogs/c.skillcatalog.json | Shipped |
        """;

    /// <summary>Both tables are read, each row in file order with its cells.</summary>
    [Fact]
    public void Read_BothTables_ReturnsRowsInFileOrder()
    {
        Registry registry = RegistryReader.Read(TwoTables, Dir, "DESK-01");

        Assert.Equal(["packs/a.skillpack.json", "packs/b.skillpack.json"], registry.Packs.Select(static row => row.LocalPath.FirstText));
        RegistryRow catalog = Assert.Single(registry.Catalogs);
        Assert.Equal("catalogs/c.skillcatalog.json", catalog.LocalPath.FirstText);
        Assert.Equal("https://github.com/o/r", registry.Packs[0].OriginUrl);
        Assert.Equal(new DateOnly(2026, 10, 1), registry.Packs[0].LastUpdated);
        Assert.Null(registry.Packs[1].OriginUrl);
        Assert.Equal([0, 1], registry.Packs.Select(static row => row.Index));
        Assert.Empty(registry.Findings);
    }

    /// <summary>Columns may come in any order; they are matched by header, ignoring case.</summary>
    [Fact]
    public void Read_ColumnsInAnyOrder_MapsByHeader()
    {
        const string text = """
            ## Packs

            | notes | LOCAL PATH | origin url |
            | --- | --- | --- |
            | n | x.skillpack.json | https://example.com/o/r |
            """;

        RegistryRow row = Assert.Single(RegistryReader.Read(text, Dir, "PC").Packs);

        Assert.Equal("x.skillpack.json", row.LocalPath.FirstText);
        Assert.Equal("n", row.Notes);
        Assert.Equal("https://example.com/o/r", row.OriginUrl);
    }

    /// <summary>A path cell with <c>&lt;br&gt;</c> splits into one entry per line, computer entries included.</summary>
    [Fact]
    public void Read_PathCellWithBr_SplitsIntoEntries()
    {
        const string text = """
            ## Catalogs

            | Local path |
            | --- |
            | DESK-01: E:\Clones\a.skillcatalog.json<br>LAPTOP: C:\src\a.skillcatalog.json<br />b.skillcatalog.json |
            """;

        RegistryRow row = Assert.Single(RegistryReader.Read(text, Dir, "PC").Catalogs);

        Assert.Equal(3, row.LocalPath.Entries.Count);
        Assert.Equal(new PathEntry("DESK-01", @"E:\Clones\a.skillcatalog.json"), row.LocalPath.Entries[0]);
        Assert.Equal(new PathEntry("LAPTOP", @"C:\src\a.skillcatalog.json"), row.LocalPath.Entries[1]);
        Assert.Equal(new PathEntry(null, "b.skillcatalog.json"), row.LocalPath.Entries[2]);
    }

    /// <summary>A code span contributes its literal text, so a path with spaces survives.</summary>
    [Fact]
    public void Read_CodeSpanPath_KeepsSpaces()
    {
        const string text = """
            ## Packs

            | Local path |
            | --- |
            | `E:\a b\c.skillpack.json` |
            """;

        RegistryRow row = Assert.Single(RegistryReader.Read(text, Dir, "PC").Packs);

        Assert.Equal(@"E:\a b\c.skillpack.json", row.LocalPath.FirstText);
    }

    /// <summary>A row's span covers only its own line, 1-based.</summary>
    [Fact]
    public void Read_RowSpan_CoversOnlyTheRowLines()
    {
        Registry registry = RegistryReader.Read(TwoTables, Dir, "PC");

        string[] lines = TwoTables.Split('\n');
        foreach (RegistryRow row in registry.AllRows)
        {
            Assert.Equal(row.Span.FirstLine, row.Span.LastLine);
            Assert.Contains(row.LocalPath.FirstText, lines[row.Span.FirstLine - 1], StringComparison.Ordinal);
        }

        Assert.Equal(9, registry.Packs[0].Span.FirstLine);
    }

    /// <summary>Empty text has no table: R001, no rows.</summary>
    [Fact]
    public void Read_EmptyText_R001()
    {
        Registry registry = RegistryReader.Read(string.Empty, Dir, "PC");

        Finding finding = Assert.Single(registry.Findings);
        Assert.Equal("R001", finding.Code);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(registry.FilePath, finding.File);
        Assert.Empty(registry.Packs);
    }

    /// <summary>Binary garbage reads as no table: R001, and nothing throws.</summary>
    [Fact]
    public void Read_BinaryGarbage_R001AndNoThrow()
    {
        Random random = new(7);
        byte[] bytes = new byte[4096];
        random.NextBytes(bytes);

        Registry registry = RegistryReader.Read(Encoding.Latin1.GetString(bytes), Dir, "PC");

        Assert.Contains(registry.Findings, static finding => finding.Code == "R001");
    }

    /// <summary>A Packs table with no Local path column is R002 and its rows are not read.</summary>
    [Fact]
    public void Read_TableWithoutLocalPath_R002()
    {
        const string text = """
            ## Packs

            | Path | Notes |
            | --- | --- |
            | a.skillpack.json | x |
            """;

        Registry registry = RegistryReader.Read(text, Dir, "PC");

        Assert.Equal("R002", Assert.Single(registry.Findings).Code);
        Assert.Empty(registry.Packs);
    }

    /// <summary>Load checks the length first: over 1 MB is R011, no rows, and the text is never read.</summary>
    [Fact]
    public void Read_OverOneMegabyte_R011AndNoRows()
    {
        InMemoryFileReader files = new InMemoryFileReader().Add(Path.Combine(Dir, "SKILLS.md"), new string('x', (1024 * 1024) + 1));

        Registry registry = RegistryReader.Load(files, Dir, "PC");

        Assert.Equal("R011", Assert.Single(registry.Findings).Code);
        Assert.Empty(registry.Packs);
        Assert.Equal(0, files.Reads);
    }

    /// <summary>A second Packs heading is R012 and its table is ignored.</summary>
    [Fact]
    public void Read_SecondPacksHeading_R012()
    {
        const string text = """
            ## Packs

            | Local path |
            | --- |
            | a.skillpack.json |

            ## Packs

            | Local path |
            | --- |
            | b.skillpack.json |
            """;

        Registry registry = RegistryReader.Read(text, Dir, "PC");

        Assert.Equal("a.skillpack.json", Assert.Single(registry.Packs).LocalPath.FirstText);
        Finding finding = Assert.Single(registry.Findings);
        Assert.Equal("R012", finding.Code);
        Assert.Equal(Severity.Warning, finding.Severity);
    }

    /// <summary>A missing file is R001 from Load.</summary>
    [Fact]
    public void Load_MissingFile_R001()
    {
        Registry registry = RegistryReader.Load(new InMemoryFileReader(), Dir, "PC");

        Assert.Equal("R001", Assert.Single(registry.Findings).Code);
    }

    /// <summary>A table that is not under a Packs or Catalogs heading is ignored, and one under a level-1 heading break is not taken.</summary>
    [Fact]
    public void Read_OtherTables_AreIgnored()
    {
        const string text = """
            ## Notes

            | Local path |
            | --- |
            | ignored.skillpack.json |

            ## Packs

            Nothing here yet.

            # Appendix

            | Local path |
            | --- |
            | also-ignored.skillpack.json |
            """;

        Registry registry = RegistryReader.Read(text, Dir, "PC");

        Assert.Empty(registry.Packs);
        Assert.Equal("R001", Assert.Single(registry.Findings).Code);
    }

    /// <summary>Row findings: an empty path is R003, a bad date R006, a bad origin R007, a doubled computer R010, two plain entries R013.</summary>
    [Fact]
    public void Read_RowProblems_RaiseRowFindings()
    {
        const string text = """
            ## Packs

            | Local path | Origin URL | Last updated |
            | --- | --- | --- |
            | | | |
            | a.skillpack.json | www.example.com | 17/10/2026 |
            | PC-1: a.json<br>pc-1: b.json | | |
            | a.json<br>b.json | | |
            """;

        Registry registry = RegistryReader.Read(text, Dir, "PC");

        Assert.Equal(["R003", "R006", "R007", "R010", "R013"], registry.Findings.Select(static finding => finding.Code).Order(StringComparer.Ordinal));
        Assert.Equal(4, registry.Packs.Count);
    }

    /// <summary>The reader never throws on random text.</summary>
    [Fact]
    public void Read_RandomStrings_NeverThrows()
    {
        Random random = new(11);
        const string alphabet = "|-#:\\/`*_<>br \n\tPacksCatalogsLocal path";
        for (int i = 0; i < 1000; i++)
        {
            char[] chars = new char[random.Next(0, 200)];
            for (int c = 0; c < chars.Length; c++)
            {
                chars[c] = alphabet[random.Next(alphabet.Length)];
            }

            Registry registry = RegistryReader.Read(new string(chars), Dir, "PC");

            Assert.NotNull(registry);
        }
    }
}
