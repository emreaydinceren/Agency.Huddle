using System.Text;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins Spec §6.14 "What the Agent is told": <see cref="LibraryDocumentCollector"/> collects,
/// dedupes, caps, labels and inlines the Library documents mentioned across a Turn's message texts.</summary>
public sealed class LibraryDocumentCollectorTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    /// <summary>Builds an isolated fixture and an <see cref="IOptions{TOptions}"/> that reflects the
    /// same, possibly-mutated <see cref="TeamOptions"/> instance the fixture's <see cref="LibraryFileService"/>
    /// is constructed with, so a collector built from it sees identical caps.</summary>
    private static (LibraryFileServiceFixture Fixture, IOptions<TeamOptions> Options) BuildFixture(Action<TeamOptions>? configure = null)
    {
        TeamOptions captured = new();
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(options =>
        {
            configure?.Invoke(options);
            captured = options;
        });
        return (fixture, Options.Create(captured));
    }

    /// <summary>Documents mentioned in several messages, in the order and text form given, are collected
    /// once each in first-seen order.</summary>
    [Fact]
    public async Task Collect_PathsInMessages_FirstSeenOrder_Deduped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string second = Path.Combine(pinnedRoot, "second.md");
        string first = Path.Combine(pinnedRoot, "first.md");
        File.WriteAllText(second, "second");
        File.WriteAllText(first, "first");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see {new Uri(second).AbsoluteUri} and {new Uri(first).AbsoluteUri}"],
            readsFiles: true,
            ct);

        Assert.Equal(2, report.Items.Count);
        Assert.Equal(second, report.Items[0].FullPath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(first, report.Items[1].FullPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The same on-disk file, written two different ways across three separate message texts,
    /// collects as exactly one item, at its first-seen position.</summary>
    [Fact]
    public async Task Collect_DedupedAcrossMessages_SamePathTwoForms_SingleItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "plan.md");
        File.WriteAllText(filePath, "text");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);
        string fileUrl = new Uri(filePath).AbsoluteUri;

        LibraryDocumentsReport report = await collector.CollectAsync(
            ["first message, no path here", $"a link: {fileUrl}", $"same file again: {fileUrl}"],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(filePath, item.FullPath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, report.NotListed);
    }

    /// <summary>Every §6.14 location shape: Team+Project, Team alone, a Teammate's Work Dir, a Teammate's
    /// definition file ("teammate {Name}", corrections-B6 item 4) and a pinned root.</summary>
    [Fact]
    public async Task Collect_LocationLabels()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;

        string teamOnlyDir = Path.Combine(fixture.DataDir, "Teams", "Marketing");
        Directory.CreateDirectory(teamOnlyDir);
        string teamOnlyFile = Path.Combine(teamOnlyDir, "roadmap.md");
        File.WriteAllText(teamOnlyFile, "roadmap");

        string projectDir = Path.Combine(teamOnlyDir, "Launch Q4");
        Directory.CreateDirectory(projectDir);
        string projectFile = Path.Combine(projectDir, "plan.md");
        File.WriteAllText(projectFile, "plan");

        fixture.CreateTeammate("nova", "Nova", "nova");
        TeammatePaths teammatePaths = new(options);
        string workDir = teammatePaths.WorkDir("nova");
        Directory.CreateDirectory(workDir);
        string workDirFile = Path.Combine(workDir, "notes.md");
        File.WriteAllText(workDirFile, "notes");
        string definitionFile = teammatePaths.DefinitionFile("nova");

        string pinnedRoot = fixture.CreatePinnedRoot("Huddle docs");
        string pinnedFile = Path.Combine(pinnedRoot, "readme.md");
        File.WriteAllText(pinnedFile, "readme");

        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);
        string[] texts =
        [
            new Uri(teamOnlyFile).AbsoluteUri,
            new Uri(projectFile).AbsoluteUri,
            new Uri(workDirFile).AbsoluteUri,
            new Uri(definitionFile).AbsoluteUri,
            new Uri(pinnedFile).AbsoluteUri,
        ];

        LibraryDocumentsReport report = await collector.CollectAsync(texts, readsFiles: true, ct);

        Assert.Equal(5, report.Items.Count);
        Assert.Equal("Team Marketing", report.Items[0].Location, StringComparer.Ordinal);
        Assert.Equal("Team Marketing, Project Launch Q4", report.Items[1].Location, StringComparer.Ordinal);
        Assert.Equal("Nova's Work Dir", report.Items[2].Location, StringComparer.Ordinal);
        Assert.Equal("teammate Nova", report.Items[3].Location, StringComparer.Ordinal);
        Assert.Equal("pinned root \"Huddle docs\"", report.Items[4].Location, StringComparer.Ordinal);
    }

    /// <summary>A document's <see cref="LibraryDocumentItem.Size"/> is the shared Library size format.</summary>
    [Fact]
    public async Task Collect_SizeUsesLibrarySize()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "sized.txt");
        File.WriteAllText(filePath, new string('a', 3000));
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal("3 KB", item.Size, StringComparer.Ordinal);
        Assert.Equal(LibrarySize.Format(item.Length), item.Size, StringComparer.Ordinal);
    }

    /// <summary>A path outside every Library Root, and a path inside one to a file that does not exist,
    /// both add nothing.</summary>
    [Fact]
    public async Task Collect_OutsideOrMissing_Ignored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string missing = Path.Combine(pinnedRoot, "missing.md");
        string outsideDir = Path.Combine(Path.GetTempPath(), "huddle-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            string outsideFile = Path.Combine(outsideDir, "outside.md");
            File.WriteAllText(outsideFile, "outside");
            LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

            LibraryDocumentsReport report = await collector.CollectAsync(
                [$"{new Uri(missing).AbsoluteUri} {new Uri(outsideFile).AbsoluteUri}"],
                readsFiles: true,
                ct);

            Assert.Empty(report.Items);
            Assert.Equal(0, report.NotListed);
            Assert.True(report.IsEmpty);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    /// <summary>A file under a Team's hidden <c>_tasks</c> subtree is never listed.</summary>
    [Fact]
    public async Task Collect_HiddenTasksFile_Ignored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string tasksDir = Path.Combine(fixture.DataDir, "Teams", "Marketing", "_tasks");
        Directory.CreateDirectory(tasksDir);
        string tasksFile = Path.Combine(tasksDir, "task.md");
        File.WriteAllText(tasksFile, "task");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(tasksFile).AbsoluteUri],
            readsFiles: true,
            ct);

        Assert.Empty(report.Items);
    }

    /// <summary>More documents than <see cref="LibraryOptions.MaxReferencedDocuments"/> are mentioned: the
    /// first N are kept, in order, and the rest only counted.</summary>
    [Fact]
    public async Task Collect_OverCap_CountsRest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        List<string> urls = [];
        for (int i = 0; i < 12; i++)
        {
            string path = Path.Combine(pinnedRoot, $"doc{i:D2}.md");
            File.WriteAllText(path, "text");
            urls.Add(new Uri(path).AbsoluteUri);
        }

        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync([string.Join(' ', urls)], readsFiles: true, ct);

        Assert.Equal(10, report.Items.Count);
        Assert.Equal(2, report.NotListed);
        Assert.False(report.IsEmpty);
    }

    /// <summary>An Agent on a file-reading Adapter gets the location line only: no inlined text, whatever
    /// the document's size.</summary>
    [Fact]
    public async Task Collect_ReadsFiles_NoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "small.md");
        File.WriteAllText(filePath, "hello library");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Null(item.Text);
        Assert.False(item.Truncated);
    }

    /// <summary>An Agent without file tools gets a small Markdown document's decoded text inlined whole.</summary>
    [Fact]
    public async Task Collect_NoReadsFiles_InlinesText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "small.md");
        File.WriteAllText(filePath, "hello library");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: false,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        LibraryDocumentItem expected = new(filePath, "pinned root \"Docs\"", LibrarySize.Format(13), "hello library", Truncated: false, Length: 13, TooLarge: false);
        Assert.Equal(expected, item);
    }

    /// <summary>A document larger than <see cref="LibraryOptions.MaxInlineBytes"/> is inlined cut at that
    /// many UTF-8 bytes, on a whole-rune boundary, with <c>Truncated</c> set.</summary>
    [Fact]
    public async Task Collect_NoReadsFiles_OverMaxInline_Truncates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture(o => o.Library.MaxInlineBytes = 100);
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "big.md");
        string longText = new string('a', 500);
        File.WriteAllText(filePath, longText);
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: false,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.NotNull(item.Text);
        Assert.Equal(100, Encoding.UTF8.GetByteCount(item.Text));
        Assert.Equal(new string('a', 100), item.Text, StringComparer.Ordinal);
        Assert.True(item.Truncated);
    }

    /// <summary>A 4-byte emoji straddling <see cref="LibraryOptions.MaxInlineBytes"/> is cut before the
    /// emoji entirely, never inside its UTF-8 sequence (corrections-B6 item 6).</summary>
    [Fact]
    public async Task Collect_NoReadsFiles_TruncationCutsBeforeEmoji()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture(o => o.Library.MaxInlineBytes = 10);
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "emoji.md");
        const string text = "1234567\U0001F600rest";
        File.WriteAllText(filePath, text);
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: false,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal("1234567", item.Text, StringComparer.Ordinal);
        Assert.True(item.Truncated);
    }

    /// <summary>Only text files are inlined: an image gets the location line only.</summary>
    [Fact]
    public async Task Collect_NoReadsFiles_Image_NoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "picture.png");
        File.WriteAllBytes(filePath, LibraryDocumentCollectorTests.PngHead);
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: false,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        LibraryDocumentItem expected = new(filePath, "pinned root \"Docs\"", LibrarySize.Format(LibraryDocumentCollectorTests.PngHead.Length), Text: null, Truncated: false, Length: LibraryDocumentCollectorTests.PngHead.Length, TooLarge: false);
        Assert.Equal(expected, item);
    }

    /// <summary>A document over <see cref="LibraryOptions.MaxEditableBytes"/> gets no text (corrections-B5
    /// item 31; corrections-B6 item 3): the caller renders the too-large line from its Length.</summary>
    [Fact]
    public async Task Collect_NoReadsFiles_OverMaxEditableBytes_NoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture(o => o.Library.MaxEditableBytes = 50);
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "huge.md");
        string longText = new string('a', 200);
        File.WriteAllText(filePath, longText);
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [new Uri(filePath).AbsoluteUri],
            readsFiles: false,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        LibraryDocumentItem expected = new(filePath, "pinned root \"Docs\"", LibrarySize.Format(200), Text: null, Truncated: false, Length: 200, TooLarge: true);
        Assert.Equal(expected, item);
    }

    /// <summary>A path with a space, written as a <c>file:</c> URL in plain text, is found (corrections-B6
    /// item 2): a positive row that passes on every OS.</summary>
    [Fact]
    public async Task Collect_PlainText_PathWithSpace_FileUrl_Found()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string projectDir = Path.Combine(pinnedRoot, "Launch Q4");
        Directory.CreateDirectory(projectDir);
        string filePath = Path.Combine(projectDir, "plan.md");
        File.WriteAllText(filePath, "plan");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see {new Uri(filePath).AbsoluteUri} for the plan"],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(filePath, item.FullPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The same space-containing path, written as a bare Windows drive path in plain text, is
    /// found on Windows (skipped elsewhere: a drive path is not rooted off Windows).</summary>
    [Fact]
    public async Task Collect_PlainText_PathWithSpace_DrivePath_Found()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("A Windows drive path is only meaningfully absolute on Windows.");
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string projectDir = Path.Combine(pinnedRoot, "Launch Q4");
        Directory.CreateDirectory(projectDir);
        string filePath = Path.Combine(projectDir, "plan.md");
        File.WriteAllText(filePath, "plan");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see {filePath} for the plan"],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(filePath, item.FullPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The same space-containing path, in a backtick code span, matches whole through
    /// <see cref="LibraryPathPatterns.TryMatchWhole"/>.</summary>
    [Fact]
    public async Task Collect_CodeSpan_PathWithSpace_Found()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string projectDir = Path.Combine(pinnedRoot, "Launch Q4");
        Directory.CreateDirectory(projectDir);
        string filePath = Path.Combine(projectDir, "plan.md");
        File.WriteAllText(filePath, "plan");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see `{new Uri(filePath).AbsoluteUri}` for the plan"],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(filePath, item.FullPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>When two candidate ends both exist on disk (<c>a b.md</c> and <c>a b.md.bak</c>), the
    /// longer of the two that the mentioned text actually spells is the one kept.</summary>
    [Fact]
    public async Task Collect_PlainText_ShorterCandidateAlsoExists_KeepsLongest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string shortPath = Path.Combine(pinnedRoot, "a b.md");
        string longPath = Path.Combine(pinnedRoot, "a b.md.bak");
        File.WriteAllText(shortPath, "short");
        File.WriteAllText(longPath, "long");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see {longPath}"],
            readsFiles: true,
            ct);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("A bare Windows drive path is not rooted off Windows.");
        }

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(longPath, item.FullPath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>More than eight known-extension candidate ends in a line never throws: the probe stays
    /// bounded at eight candidates, so a real file past the eighth boundary is not found.</summary>
    [Fact]
    public async Task Collect_PlainText_MoreThanEightCandidateEnds_BoundedNoException()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("A bare Windows drive path is not rooted off Windows.");
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");

        // Nine known-extension boundaries (".csv" x8 then ".md"): only the real file, at the 9th
        // boundary, exists on disk - past the documented 8-candidate cap.
        string name = "report" + string.Concat(Enumerable.Repeat(".csv", 8)) + ".md";
        string filePath = Path.Combine(pinnedRoot, name);
        File.WriteAllText(filePath, "report");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);

        LibraryDocumentsReport report = await collector.CollectAsync([$"see {filePath} today"], readsFiles: true, ct);

        Assert.Empty(report.Items);
    }

    /// <summary>A mentioned path immediately followed by a full stop or closing parenthesis is still
    /// found: the trailing punctuation is trimmed.</summary>
    [Fact]
    public async Task Collect_PathFollowedByDotOrParen_Trimmed_Found()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, IOptions<TeamOptions> options) = BuildFixture();
        using LibraryFileServiceFixture _ = fixture;
        string pinnedRoot = fixture.CreatePinnedRoot("Docs");
        string filePath = Path.Combine(pinnedRoot, "plan.md");
        File.WriteAllText(filePath, "plan");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), options);
        string url = new Uri(filePath).AbsoluteUri;

        LibraryDocumentsReport report = await collector.CollectAsync(
            [$"see ({url}).", $"see {url}."],
            readsFiles: true,
            ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(filePath, item.FullPath, StringComparer.OrdinalIgnoreCase);
    }
}
