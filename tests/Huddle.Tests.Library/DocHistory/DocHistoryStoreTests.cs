using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins <see cref="DocHistoryStore"/> (DocHistory spec §6.2): layout, line format, tolerance of bad lines, the entry cap, moves and the <c>Changed</c> event.</summary>
public sealed class DocHistoryStoreTests
{
    /// <summary>A document with no history file reads as an empty list.</summary>
    [Fact]
    public void Read_MissingFile_IsEmpty()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);

        Assert.Empty(store.Read(Document("findings.md")));
    }

    /// <summary>Appended entries read back whole, oldest first.</summary>
    [Fact]
    public void Append_ThenRead_RoundTripsInOrder()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        DocHistoryEntry first = At(9, 12, "Miriam", DocChangeKind.Created, "First draft → four findings");
        DocHistoryEntry second = At(11, 2, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);

        store.Append(file, first);
        store.Append(file, second);

        Assert.Equal([first, second], store.Read(file));
    }

    /// <summary>The history file mirrors the Library tree under <c>doc-history/{rootId}</c> with a <c>.jsonl</c> extension.</summary>
    [Fact]
    public void Append_WritesMirroredJsonLinesFile()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);

        store.Append(Document("Research/findings.md"), At(9, 12, "Miriam", DocChangeKind.Created, "Draft"));

        string expected = Path.Combine(dataDir.Path, "doc-history", "vault", "Research", "findings.md.jsonl");
        Assert.True(File.Exists(expected));
    }

    /// <summary>A line is one camelCase JSON object with the format version, written readably and ended by <c>\n</c>.</summary>
    [Fact]
    public void Append_WritesTheSpecifiedLineFormat()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");

        store.Append(file, At(11, 2, "Mira", DocChangeKind.Edited, "Downgraded F1 → moderate"));

        Assert.Equal(
            """{"v":1,"at":"2026-10-18T11:02:00+00:00","actor":"Mira","actorKind":"agent","kind":"edited","summary":"Downgraded F1 → moderate"}""" + "\n",
            File.ReadAllText(store.FileFor(file)));
    }

    /// <summary>A line that is not JSON, a line with another format version and a blank line are skipped; the readable lines still read.</summary>
    [Fact]
    public void Read_SkipsUnreadableLines()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        DocHistoryEntry good = At(9, 12, "Miriam", DocChangeKind.Created, "Draft");
        store.Append(file, good);
        File.AppendAllText(store.FileFor(file), "not json\n\n" + """{"v":2,"at":"2026-10-18T10:00:00+00:00","actor":"Ruth","actorKind":"agent","kind":"reviewed","summary":"x"}""" + "\n");

        Assert.Equal([good], store.Read(file));
    }

    /// <summary>A line missing its actor or naming an unknown kind is not an entry.</summary>
    /// <param name="line">The damaged line.</param>
    [Theory]
    [InlineData("""{"v":1,"at":"2026-10-18T10:00:00+00:00","actorKind":"agent","kind":"edited","summary":"x"}""")]
    [InlineData("""{"v":1,"at":"2026-10-18T10:00:00+00:00","actor":"Ruth","actorKind":"agent","kind":"approved","summary":"x"}""")]
    [InlineData("""{"v":1,"at":"2026-10-18T10:00:00+00:00","actor":"Ruth","actorKind":"robot","kind":"edited","summary":"x"}""")]
    public void TryParseLine_DamagedLine_IsRejected(string line)
    {
        Assert.False(DocHistoryStore.TryParseLine(line, out _));
    }

    /// <summary>A line with no summary reads as an empty summary, as a Human save is written.</summary>
    [Fact]
    public void TryParseLine_NoSummary_IsEmpty()
    {
        Assert.True(DocHistoryStore.TryParseLine("""{"v":1,"at":"2026-10-18T10:00:00+00:00","actor":"Emre","actorKind":"human","kind":"edited"}""", out DocHistoryEntry? entry));
        Assert.Equal(string.Empty, entry.Summary);
    }

    /// <summary>Past <see cref="DocHistoryOptions.MaxEntries"/>, the oldest entries are dropped and the newest kept.</summary>
    [Fact]
    public void Append_OverMaxEntries_KeepsTheNewest()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(Microsoft.Extensions.Options.Options.Create(new Agency.Huddle.App.TeamOptions { DataDir = dataDir.Path, DocHistory = { MaxEntries = 2 } }), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        DocHistoryEntry first = At(9, 0, "A", DocChangeKind.Created, "1");
        DocHistoryEntry second = At(10, 0, "B", DocChangeKind.Edited, "2");
        DocHistoryEntry third = At(11, 0, "C", DocChangeKind.Edited, "3");

        store.Append(file, first);
        store.Append(file, second);
        store.Append(file, third);

        Assert.Equal([second, third], store.Read(file));
    }

    /// <summary>Moving a document moves its history file; the old path reads empty.</summary>
    [Fact]
    public void Move_Document_CarriesItsHistory()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        DocHistoryEntry entry = At(9, 12, "Miriam", DocChangeKind.Created, "Draft");
        store.Append(Document("findings.md"), entry);

        store.Move(Document("findings.md"), Document("Final/final-findings.md"));

        Assert.Empty(store.Read(Document("findings.md")));
        Assert.Equal([entry], store.Read(Document("Final/final-findings.md")));
    }

    /// <summary>Moving a folder moves the history of every document under it, replacing a stale history at the destination.</summary>
    [Fact]
    public void Move_Folder_CarriesEveryDocumentAndReplacesStale()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        DocHistoryEntry entry = At(9, 12, "Miriam", DocChangeKind.Created, "Draft");
        store.Append(Document("Research/deep/findings.md"), entry);
        store.Append(Document("Archive/deep/findings.md"), At(8, 0, "Old", DocChangeKind.Created, "stale"));

        store.Move(Folder("Research"), Folder("Archive"));

        Assert.Equal([entry], store.Read(Document("Archive/deep/findings.md")));
        Assert.Empty(store.Read(Document("Research/deep/findings.md")));
    }

    /// <summary>Moving an item with no history changes nothing and raises nothing.</summary>
    [Fact]
    public void Move_NoHistory_IsANoOp()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        List<LibraryPath> changed = [];
        store.Changed += changed.Add;

        store.Move(Document("a.md"), Document("b.md"));

        Assert.Empty(changed);
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "doc-history")));
    }

    /// <summary><c>Changed</c> fires once for an append, and for both paths of a move.</summary>
    [Fact]
    public void Changed_FiresForAppendAndBothMovePaths()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        List<string> changed = [];
        store.Changed += path => changed.Add(path.RelativePath);

        store.Append(Document("a.md"), At(9, 0, "A", DocChangeKind.Created, "x"));
        store.Move(Document("a.md"), Document("b.md"));

        Assert.Equal(["a.md", "a.md", "b.md"], changed);
    }

    /// <summary>A case-only rename of a folder keeps its history: the destination is the source on a case-insensitive file system, so it must not be deleted first.</summary>
    [Fact]
    public void Move_Folder_CaseOnlyRename_KeepsHistory()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        store.Append(Document("Notes/a.md"), At(9, 0, "Mira", DocChangeKind.Created, "x"));
        string parent = Path.GetDirectoryName(Path.GetDirectoryName(store.FileFor(Document("Notes/a.md"))))!;

        store.Move(Folder("Notes"), Folder("notes"));

        Assert.Single(store.Read(Document("notes/a.md")));
        Assert.Equal(["notes"], Directory.GetDirectories(parent).Select(Path.GetFileName));
    }

    /// <summary>A case-only rename of a document keeps its history file under the new case.</summary>
    [Fact]
    public void Move_Document_CaseOnlyRename_KeepsHistory()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        store.Append(Document("Findings.md"), At(9, 0, "Mira", DocChangeKind.Created, "x"));

        store.Move(Document("Findings.md"), Document("findings.md"));

        Assert.Single(store.Read(Document("findings.md")));
        Assert.Equal(["findings.md.jsonl"], Directory.GetFiles(Path.GetDirectoryName(store.FileFor(Document("findings.md")))!).Select(Path.GetFileName));
    }

    /// <summary>A history file another process holds open reads as empty instead of throwing, so the document still opens.</summary>
    [Fact]
    public void Read_LockedFile_IsEmpty()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Created, "x"));
        using FileStream held = new(store.FileFor(Document("findings.md")), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        IReadOnlyList<DocHistoryEntry> entries = store.Read(Document("findings.md"));

        Assert.Empty(entries);
    }

    /// <summary>An entry appended after a torn last line is kept: the torn line is skipped, not fused with the new one.</summary>
    [Fact]
    public void Append_AfterATornLastLine_KeepsTheNewEntry()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        DocHistoryEntry first = At(9, 0, "Mira", DocChangeKind.Created, "first");
        store.Append(file, first);
        File.AppendAllText(store.FileFor(file), "{\"v\":1,\"at\":\"2026-10-18T10:00");
        DocHistoryEntry second = At(11, 0, "Ruth", DocChangeKind.Reviewed, "second");

        store.Append(file, second);

        Assert.Equal([first, second], store.Read(file));
    }
}
