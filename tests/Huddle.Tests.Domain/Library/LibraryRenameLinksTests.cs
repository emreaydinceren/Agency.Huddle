using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for the D8 rename/move link rewrite (Spec §6.5 <i>Rename or move rewrite</i> steps 1-4;
/// use case L4), wired into <see cref="LibraryFileService.RenameAsync"/> and
/// <see cref="LibraryFileService.MoveAsync"/>, and for <see cref="LibraryFileService.PreviewLinkChangesAsync"/>
/// (corrections-B5 D8 item 8).
/// </summary>
public sealed class LibraryRenameLinksTests
{
    /// <summary>Encodes <paramref name="text"/> as UTF-8 with a BOM, keeping any <c>\r\n</c> in
    /// <paramref name="text"/> literally (ADR-0028 byte-exact round trips).</summary>
    private static byte[] Utf8BomCrLf(string text) => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];

    /// <summary>The bytes a UTF-8 BOM+CRLF note would have if every occurrence of
    /// <paramref name="oldSnippet"/> in its original text were replaced with
    /// <paramref name="newSnippet"/> and nothing else changed - the byte-exact expectation for
    /// "only the link text changed" (Plan 8.5.t).</summary>
    private static byte[] ExpectedAfterLinkReplace(byte[] originalBytes, string oldSnippet, string newSnippet)
    {
        string originalText = Encoding.UTF8.GetString(originalBytes, 3, originalBytes.Length - 3);
        string expectedText = originalText.Replace(oldSnippet, newSnippet, StringComparison.Ordinal);
        return Utf8BomCrLf(expectedText);
    }

    /// <summary>Renaming a note rewrites every link to it across the root: 5 links in 3 notes, and each
    /// file's only change is its link text, checked byte-exactly on UTF-8 BOM+CRLF notes.</summary>
    [Fact]
    public async Task Rename_Note_RewritesLinksInRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] aOriginal = Utf8BomCrLf("Intro [[plan]] middle [[plan|Also]] end.\r\n");
        byte[] bOriginal = Utf8BomCrLf("See [[plan]] and also [[plan#Section]].\r\n");
        byte[] cOriginal = Utf8BomCrLf("Only [[plan]] here.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "a.md"), aOriginal);
        File.WriteAllBytes(Path.Combine(vault, "b.md"), bOriginal);
        File.WriteAllBytes(Path.Combine(vault, "c.md"), cOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(["a.md", "b.md", "c.md"], result.Value.RewrittenNotes.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(result.Value.FailedNotes);
        Assert.Equal(ExpectedAfterLinkReplace(aOriginal, "plan", "roadmap"), File.ReadAllBytes(Path.Combine(vault, "a.md")));
        Assert.Equal(ExpectedAfterLinkReplace(bOriginal, "plan", "roadmap"), File.ReadAllBytes(Path.Combine(vault, "b.md")));
        Assert.Equal(ExpectedAfterLinkReplace(cOriginal, "plan", "roadmap"), File.ReadAllBytes(Path.Combine(vault, "c.md")));
    }

    /// <summary>Before anything moves, the preview reports the same counts the rename will act on.</summary>
    [Fact]
    public async Task PreviewLinkChanges_CountsLinksAndNotes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "a.md"), Utf8BomCrLf("Intro [[plan]] middle [[plan|Also]] end.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "b.md"), Utf8BomCrLf("See [[plan]] and also [[plan#Section]].\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "c.md"), Utf8BomCrLf("Only [[plan]] here.\r\n"));
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");
        LibraryPath vaultRoot = fixture.Resolve(vault, string.Empty);

        LibraryLinkPreview preview = await service.PreviewLinkChangesAsync(plan, vaultRoot, ct);

        Assert.Equal(5, preview.Links);
        Assert.Equal(3, preview.Notes);
        Assert.True(preview.IndexAvailable);
        Assert.True(File.Exists(Path.Combine(vault, "plan.md")));
    }

    /// <summary>Moving a folder rewrites links from other notes into every note that lived inside it,
    /// written with a root-anchored path, so their target segments now differ.</summary>
    [Fact]
    public async Task Move_Folder_RewritesLinksToEveryNoteInside()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Docs"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllBytes(Path.Combine(vault, "Docs", "doc1.md"), Utf8BomCrLf("Doc one.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "Docs", "doc2.md"), Utf8BomCrLf("Doc two.\r\n"));
        byte[] indexOriginal = Utf8BomCrLf("See [[/Docs/doc1]] and [[/Docs/doc2]].\r\n");
        File.WriteAllBytes(Path.Combine(vault, "index.md"), indexOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath docs = fixture.Resolve(vault, "Docs");
        LibraryPath archive = fixture.Resolve(vault, "Archive");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(docs, archive, ct);

        Assert.NotNull(result.Value);
        Assert.Contains("index.md", result.Value.RewrittenNotes);
        byte[] indexAfter = File.ReadAllBytes(Path.Combine(vault, "index.md"));
        byte[] expected = ExpectedAfterLinkReplace(indexOriginal, "[[/Docs/doc1]]", "[[doc1]]");
        expected = ExpectedAfterLinkReplace(expected, "[[/Docs/doc2]]", "[[doc2]]");
        Assert.Equal(expected, indexAfter);
    }

    /// <summary>After a rename makes a bare target ambiguous, the rewrite writes the shortest unique
    /// form (<c>Website/plan</c>), not the bare name.</summary>
    [Fact]
    public async Task Rename_LinkNowAmbiguous_UsesLongerTarget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Website"));
        File.WriteAllBytes(Path.Combine(vault, "Website", "plan.md"), Utf8BomCrLf("Website plan.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "roadmap.md"), Utf8BomCrLf("Roadmap body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[Website/plan]] for the site plan.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath roadmap = fixture.Resolve(vault, "roadmap.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(roadmap, "plan.md", ct);

        Assert.NotNull(result.Value);
        Assert.Contains("linker.md", result.Value.RewrittenNotes);
        byte[] linkerAfter = File.ReadAllBytes(Path.Combine(vault, "linker.md"));
        string linkerText = Encoding.UTF8.GetString(linkerAfter, 3, linkerAfter.Length - 3);
        // contains-ok: the byte-exact assertion is the earlier Assert.Contains("linker.md", ...RewrittenNotes);
        // this only pins the qualifying-form target text that this scenario is about.
        Assert.Contains("[[Website/plan]]", linkerText, StringComparison.Ordinal);
    }

    /// <summary>A note held open without <see cref="FileShare.Delete"/> can't be rewritten: it is listed
    /// with the settled save-failure text, and the move itself still succeeds (step 4).</summary>
    [Fact]
    public async Task Rename_NoteFailsToWrite_ListedAndMoveKept()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("FileShare.Delete semantics differ off Windows.");
            return;
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[plan]] here.\r\n");
        string linkerPath = Path.Combine(vault, "linker.md");
        File.WriteAllBytes(linkerPath, linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        using (new FileStream(linkerPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

            Assert.NotNull(result.Value);
            Assert.True(File.Exists(Path.Combine(vault, "roadmap.md")));
            Assert.Contains(result.Value.FailedNotes, failure => failure.RelativePath == "linker.md");
            Assert.Equal("Couldn't save linker.md: it's in use or read-only.",
                result.Value.FailedNotes.Single(failure => failure.RelativePath == "linker.md").Error);
        }

        Assert.Equal(linkerOriginal, File.ReadAllBytes(linkerPath));
    }

    /// <summary>A linking note over <see cref="LibraryOptions.MaxEditableBytes"/> is skipped and listed
    /// with the settled too-large text (Spec §10 E-7); its bytes are untouched.</summary>
    [Fact]
    public async Task Rename_OverMaxEditableBytes_Skipped_Listed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 32);
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("A very long line that links to [[plan]] and keeps going past the limit.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.RewrittenNotes);
        LibraryNoteFailure failure = Assert.Single(result.Value.FailedNotes);
        Assert.Equal("linker.md", failure.RelativePath);
        Assert.Equal("linker.md is too large to update.", failure.Error);
        Assert.Equal(linkerOriginal, File.ReadAllBytes(Path.Combine(vault, "linker.md")));
    }

    /// <summary>The TEXT of a non-Markdown file is never parsed for wikilinks: a <c>.txt</c> note
    /// containing <c>[[plan]]</c> is never rewritten (judgement 36).</summary>
    [Fact]
    public async Task Rename_TextFile_LinksNeverParsedOrRewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] notesOriginal = Utf8BomCrLf("Reminder: see [[plan]] later.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "notes.txt"), notesOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.DoesNotContain("notes.txt", result.Value.RewrittenNotes);
        Assert.DoesNotContain(result.Value.FailedNotes, failure => failure.RelativePath == "notes.txt");
        Assert.Equal(notesOriginal, File.ReadAllBytes(Path.Combine(vault, "notes.txt")));
    }

    /// <summary>Renaming a non-Markdown file rewrites its embeds elsewhere in the root, as Obsidian
    /// does (judgement 36): <c>![[diagram.png]]</c> is updated when <c>diagram.png</c> is renamed.</summary>
    [Fact]
    public async Task Rename_ImageEmbed_Rewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "diagram.png"), [0x89, 0x50, 0x4E, 0x47]);
        byte[] noteOriginal = Utf8BomCrLf("Here is the picture: ![[diagram.png]]\r\n");
        File.WriteAllBytes(Path.Combine(vault, "note.md"), noteOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath diagram = fixture.Resolve(vault, "diagram.png");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(diagram, "chart.png", ct);

        Assert.NotNull(result.Value);
        Assert.Contains("note.md", result.Value.RewrittenNotes);
        Assert.Equal(ExpectedAfterLinkReplace(noteOriginal, "diagram.png", "chart.png"), File.ReadAllBytes(Path.Combine(vault, "note.md")));
    }

    /// <summary>After a rename, the index reflects the new path without a manual invalidate: a
    /// backlinks query for the renamed note's linkers still finds them, keyed by the new note name.</summary>
    [Fact]
    public async Task Rename_InvalidatesIndex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), Utf8BomCrLf("See [[plan]] here.\r\n"));
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");
        _ = fixture.Index.Backlinks(plan);

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        LibraryPath roadmap = result.Value.NewPath;
        IReadOnlyList<LibraryBacklink> backlinks = fixture.Index.Backlinks(roadmap);
        Assert.Contains(backlinks, backlink => backlink.NotePath == "linker.md");
    }

    /// <summary>A note inside a moved folder is read and rewritten at its NEW path: its own outgoing
    /// link to a note outside the folder still resolves, and any listing keys it by the new path.</summary>
    [Fact]
    public async Task Move_LinkingNoteInsideMovedFolder_RewrittenAtNewPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Docs"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] insideOriginal = Utf8BomCrLf("Links to [[/plan]] the top-level plan.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "Docs", "inside.md"), insideOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath docs = fixture.Resolve(vault, "Docs");
        LibraryPath archive = fixture.Resolve(vault, "Archive");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(docs, archive, ct);

        Assert.NotNull(result.Value);
        Assert.DoesNotContain(result.Value.FailedNotes, failure => failure.RelativePath.StartsWith("Docs", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(vault, "Archive", "Docs", "inside.md")));
        byte[] insideAfter = File.ReadAllBytes(Path.Combine(vault, "Archive", "Docs", "inside.md"));
        string insideText = Encoding.UTF8.GetString(insideAfter, 3, insideAfter.Length - 3);
        // contains-ok: loose check on purpose - the scenario is that the link still resolves after the
        // move, not any particular rewritten spelling of the target.
        Assert.Contains("plan", insideText, StringComparison.Ordinal);
    }

    /// <summary>Moving <c>X/sub/plan.md</c> into <c>Z</c>, next to a note whose bare <c>[[plan]]</c> resolved
    /// to the closer <c>Y/plan.md</c> (distance 2, versus the moved note's pre-move distance 3), keeps that
    /// link pointing at <c>Y/plan.md</c> instead of silently adopting the now-closer <c>Z/plan.md</c>
    /// (distance 0): the rewrite re-points it to the now-necessary longer form
    /// (corrections-B5 D8 item 9(c), re-point row).</summary>
    [Fact]
    public async Task Move_RepointsLinkToKeepOriginalTarget()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "X", "sub"));
        Directory.CreateDirectory(Path.Combine(vault, "Y"));
        Directory.CreateDirectory(Path.Combine(vault, "Z"));
        File.WriteAllBytes(Path.Combine(vault, "Y", "plan.md"), Utf8BomCrLf("Y plan.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "X", "sub", "plan.md"), Utf8BomCrLf("X sub plan.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("Refers to [[plan]] which is Y's plan.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "Z", "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath xSubPlan = fixture.Resolve(vault, "X/sub/plan.md");
        LibraryPath zFolder = fixture.Resolve(vault, "Z");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(xSubPlan, zFolder, ct);

        Assert.NotNull(result.Value);
        Assert.Contains("Z/linker.md", result.Value.RewrittenNotes);
        byte[] linkerAfter = File.ReadAllBytes(Path.Combine(vault, "Z", "linker.md"));
        string linkerText = Encoding.UTF8.GetString(linkerAfter, 3, linkerAfter.Length - 3);
        // contains-ok: the byte-exact assertion right below already pins the whole file; this just names
        // the re-pointed target for the reader.
        Assert.Contains("[[Y/plan]]", linkerText, StringComparison.Ordinal);
        Assert.DoesNotContain("[[plan]]", linkerText, StringComparison.Ordinal);
        Assert.Equal(ExpectedAfterLinkReplace(linkerOriginal, "[[plan]]", "[[Y/plan]]"), linkerAfter);
    }

    /// <summary>A moved note's OWN outgoing link that would silently re-point from its new folder is
    /// rewritten: <c>X/sub/a.md</c>'s <c>[[helper]]</c> meant <c>X/sub/helper.md</c>, but from <c>Z/</c> it
    /// would find <c>Z/helper.md</c> first (corrections-B5 D8 item 9(c), the moved notes' own links).</summary>
    [Fact]
    public async Task Move_MovedNotesOwnLink_WouldRepoint_Rewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "X", "sub"));
        Directory.CreateDirectory(Path.Combine(vault, "Z"));
        File.WriteAllBytes(Path.Combine(vault, "X", "sub", "helper.md"), Utf8BomCrLf("X helper.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "Z", "helper.md"), Utf8BomCrLf("Z helper.\r\n"));
        byte[] movedOriginal = Utf8BomCrLf("Uses [[helper]] nearby.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "X", "sub", "a.md"), movedOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath moved = fixture.Resolve(vault, "X/sub/a.md");
        LibraryPath zFolder = fixture.Resolve(vault, "Z");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(moved, zFolder, ct);

        Assert.NotNull(result.Value);
        Assert.Contains("Z/a.md", result.Value.RewrittenNotes);
        Assert.Equal(ExpectedAfterLinkReplace(movedOriginal, "[[helper]]", "[[sub/helper]]"), File.ReadAllBytes(Path.Combine(vault, "Z", "a.md")));
    }

    /// <summary>A link that still resolves to the same note after the move is NOT rewritten, even when it
    /// isn't in its shortest form: <c>[[Marketing/brand-voice]]</c> stays byte-identical and its note isn't
    /// listed (the rewrite touches only links whose resolution changed).</summary>
    [Fact]
    public async Task Rename_UnaffectedLongFormLink_LeftByteIdentical()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Marketing"));
        Directory.CreateDirectory(Path.Combine(vault, "Other"));
        Directory.CreateDirectory(Path.Combine(vault, "notes"));
        File.WriteAllBytes(Path.Combine(vault, "Marketing", "brand-voice.md"), Utf8BomCrLf("Voice.\r\n"));
        byte[] longOriginal = Utf8BomCrLf("See [[Marketing/brand-voice]] first.\r\n");
        string longPath = Path.Combine(vault, "Other", "long.md");
        File.WriteAllBytes(longPath, longOriginal);
        File.WriteAllBytes(Path.Combine(vault, "notes", "a.md"), Utf8BomCrLf("Unrelated.\r\n"));
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath unrelated = fixture.Resolve(vault, "notes/a.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(unrelated, "b.md", ct);

        Assert.NotNull(result.Value);
        Assert.DoesNotContain("Other/long.md", result.Value.RewrittenNotes);
        Assert.Equal(longOriginal, File.ReadAllBytes(longPath));
    }

    /// <summary>A rename that only changes casing (<c>plan.md</c> to <c>Plan.md</c>) still rewrites links
    /// to the new casing.</summary>
    [Fact]
    public async Task Rename_CaseOnly_Rewrites()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string planPath = Path.Combine(vault, "plan.md");
        File.WriteAllBytes(planPath, Utf8BomCrLf("Plan body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[plan]] here.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), linkerOriginal);
        fixture.Reload();

        bool caseInsensitiveFileSystem = File.Exists(Path.Combine(vault, "PLAN.MD"));
        if (!caseInsensitiveFileSystem)
        {
            Assert.Skip("This file system is case-sensitive; there is no case-only twin to rename.");
            return;
        }

        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "Plan.md", ct);

        Assert.NotNull(result.Value);
        Assert.Equal("Plan.md", Path.GetFileName(result.Value.NewPath.FullPath));
        Assert.Contains("linker.md", result.Value.RewrittenNotes);
    }

    /// <summary>Moving a folder rewrites links between two notes that both lived inside it, written with
    /// a root-anchored path that named the old parent folder.</summary>
    [Fact]
    public async Task Move_Folder_InternalLinks_Rewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Docs"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllBytes(Path.Combine(vault, "Docs", "target.md"), Utf8BomCrLf("Target body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[/Docs/target]] inside.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "Docs", "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath docs = fixture.Resolve(vault, "Docs");
        LibraryPath archive = fixture.Resolve(vault, "Archive");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(docs, archive, ct);

        Assert.NotNull(result.Value);
        Assert.Contains("Archive/Docs/linker.md", result.Value.RewrittenNotes);
        byte[] linkerAfter = File.ReadAllBytes(Path.Combine(vault, "Archive", "Docs", "linker.md"));
        string linkerText = Encoding.UTF8.GetString(linkerAfter, 3, linkerAfter.Length - 3);
        Assert.DoesNotContain("/Docs/target", linkerText, StringComparison.Ordinal);
    }

    /// <summary>A note changed on disk between its read and its write (the D8 rewrite-loop test seam) is
    /// not written, is listed with the settled "changed during the rename" text, and its own new content
    /// (the change) is left intact.</summary>
    [Fact]
    public async Task Rename_NoteChangedDuringRewrite_NotWritten_Listed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        string linkerPath = Path.Combine(vault, "linker.md");
        File.WriteAllBytes(linkerPath, Utf8BomCrLf("See [[plan]] here.\r\n"));
        fixture.Reload();
        string changedContent = "See [[plan]] here.\r\nAppended after the read.\r\n";
        LibraryFileService service = fixture.CreateService(beforeRewriteWriteForTests: _ =>
        {
            File.WriteAllBytes(linkerPath, Utf8BomCrLf(changedContent));
            return Task.CompletedTask;
        });
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.RewrittenNotes);
        LibraryNoteFailure failure = Assert.Single(result.Value.FailedNotes);
        Assert.Equal("linker.md", failure.RelativePath);
        Assert.Equal("linker.md changed during the rename; update its links by hand.", failure.Error);
        Assert.Equal(Utf8BomCrLf(changedContent), File.ReadAllBytes(linkerPath));
    }

    /// <summary>A note with mixed line endings is skipped and listed rather than normalised
    /// (ADR-0028; corrections-B5 D8 item 11); its bytes are untouched.</summary>
    [Fact]
    public async Task Rename_MixedLineEndings_SkippedAndListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] mixedOriginal = Utf8BomCrLf("First line [[plan]].\r\nSecond line.\n");
        File.WriteAllBytes(Path.Combine(vault, "mixed.md"), mixedOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.RewrittenNotes);
        LibraryNoteFailure failure = Assert.Single(result.Value.FailedNotes);
        Assert.Equal("mixed.md", failure.RelativePath);
        Assert.Equal("mixed.md has mixed line endings; update its links by hand.", failure.Error);
        Assert.Equal(mixedOriginal, File.ReadAllBytes(Path.Combine(vault, "mixed.md")));
    }

    /// <summary>A link inside a Teammate definition is never rewritten (restarting the teammate would
    /// clear its memory); it is listed instead. The renamed file lives in the teammate's own work folder.</summary>
    [Fact]
    public async Task Rename_TeammateDefinitionLink_NotRewritten_Listed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string definitionText = fixture.CreateTeammate("ada", "Ada", "ada");
        string workDir = Path.Combine(fixture.DataDir, "Teammates", "ada", "work");
        Directory.CreateDirectory(workDir);
        string notesPath = Path.Combine(workDir, "notes.md");
        File.WriteAllBytes(notesPath, Utf8BomCrLf("Working notes.\r\n"));
        string definitionPath = Path.Combine(fixture.DataDir, "Teammates", "ada", "ada.md");
        byte[] definitionOriginal = Utf8BomCrLf(definitionText.Replace("\nbody", "\nSee [[notes]] for details.\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(definitionPath, definitionOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath notes = fixture.Resolve(workDir, "notes.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(notes, "journal.md", ct);

        Assert.NotNull(result.Value);
        Assert.DoesNotContain("ada/ada.md", result.Value.RewrittenNotes);
        Assert.Contains(result.Value.FailedNotes, failure => failure.RelativePath == "ada/ada.md");
        Assert.Equal("ada.md is a teammate definition; update its links by hand.",
            result.Value.FailedNotes.Single(failure => failure.RelativePath == "ada/ada.md").Error);
        Assert.Equal(definitionOriginal, File.ReadAllBytes(definitionPath));
    }

    /// <summary>When a root has too many files to index, the move still succeeds, no notes are rewritten,
    /// <see cref="LibraryMoveResult.LinksNotUpdated"/> is set, and the preview reports the index unavailable.</summary>
    [Fact]
    public async Task Rename_IndexUnavailable_MoveSucceeds_LinksNotUpdated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxIndexedFiles = 1);
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[plan]] here.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");
        LibraryPath vaultRoot = fixture.Resolve(vault, string.Empty);

        LibraryLinkPreview preview = await service.PreviewLinkChangesAsync(plan, vaultRoot, ct);
        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.False(preview.IndexAvailable);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.LinksNotUpdated);
        Assert.Empty(result.Value.RewrittenNotes);
        Assert.Empty(result.Value.FailedNotes);
        Assert.True(File.Exists(Path.Combine(vault, "roadmap.md")));
        Assert.Equal(linkerOriginal, File.ReadAllBytes(Path.Combine(vault, "linker.md")));
    }

    /// <summary>A note written straight to disk by an agent (not through the service) after the index was
    /// already built is still found and rewritten: <c>Invalidate</c> runs BEFORE <c>LinksTo</c> is read
    /// (corrections-B5 D8 item 7).</summary>
    [Fact]
    public async Task Rename_NoteWrittenOutsideServiceAfterIndexBuilt_StillRewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        fixture.Reload();
        LibraryPath planForBacklinks = fixture.Resolve(vault, "plan.md");
        _ = fixture.Index.Backlinks(planForBacklinks);

        byte[] outsideOriginal = Utf8BomCrLf("Written outside the service: [[plan]].\r\n");
        File.WriteAllBytes(Path.Combine(vault, "outside.md"), outsideOriginal);

        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Contains("outside.md", result.Value.RewrittenNotes);
        Assert.Equal(ExpectedAfterLinkReplace(outsideOriginal, "plan", "roadmap"), File.ReadAllBytes(Path.Combine(vault, "outside.md")));
    }

    /// <summary>Renaming a note through the Teams root also invalidates a pinned root whose folder overlaps
    /// it (its FullPath contains the Teams folder): a backlinks query through the pinned root afterwards
    /// reflects the new path (Spec §10 E-2).</summary>
    [Fact]
    public async Task Rename_InOverlappingPinnedRoot_InvalidatesBoth()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions =>
            teamOptions.Library.Roots = [new PinnedRootOption { Name = "All", Path = teamOptions.DataDir }]);
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        Directory.CreateDirectory(Path.Combine(teamsRoot, "Marketing"));
        File.WriteAllBytes(Path.Combine(teamsRoot, "Marketing", "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        File.WriteAllBytes(Path.Combine(teamsRoot, "linker.md"), Utf8BomCrLf("See [[plan]] here.\r\n"));
        string pinned = fixture.DataDir;
        fixture.Reload();

        LibraryPath planViaPinned = fixture.Resolve(pinned, "Teams/Marketing/plan.md");
        _ = fixture.Index.Backlinks(planViaPinned);

        LibraryFileService service = fixture.CreateService();
        LibraryPath planViaTeams = fixture.ResolveTeams("Marketing/plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(planViaTeams, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        LibraryPath roadmapViaPinned = fixture.Resolve(pinned, "Teams/Marketing/roadmap.md");
        IReadOnlyList<LibraryBacklink> backlinks = fixture.Index.Backlinks(roadmapViaPinned);
        Assert.Contains(backlinks, backlink => backlink.NotePath == "Teams/linker.md");
    }

    /// <summary>A refused rename (destination already exists) leaves every note's bytes untouched: the
    /// rewrite never runs when the move itself is refused.</summary>
    [Fact]
    public async Task Rename_Refused_NothingRewritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        File.WriteAllBytes(Path.Combine(vault, "roadmap.md"), Utf8BomCrLf("Roadmap body.\r\n"));
        byte[] linkerOriginal = Utf8BomCrLf("See [[plan]] here.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "linker.md"), linkerOriginal);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("\"roadmap.md\" already exists here.", result.Error);
        Assert.True(File.Exists(Path.Combine(vault, "plan.md")));
        Assert.Equal(linkerOriginal, File.ReadAllBytes(Path.Combine(vault, "linker.md")));
    }

    /// <summary>An unrelated Teammate definition, an unrelated too-large note and an unrelated
    /// mixed-line-ending note - none linking to the moved item - are never listed: the listing rules for
    /// those three shapes only apply once a note is known to have an edit (8.5.i-b fix).</summary>
    [Fact]
    public async Task Rename_UnrelatedSkippableNotes_NotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 32);
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));

        string definitionText = fixture.CreateTeammate("ada", "Ada", "ada");
        string definitionPath = Path.Combine(fixture.DataDir, "Teammates", "ada", "ada.md");
        byte[] definitionOriginal = Utf8BomCrLf(definitionText.Replace("\nbody", "\nUnrelated, no links here.\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(definitionPath, definitionOriginal);

        byte[] tooLargeOriginal = Utf8BomCrLf("An unrelated very long line that never links to anything, just padding out past the limit.\r\n");
        File.WriteAllBytes(Path.Combine(vault, "big.md"), tooLargeOriginal);

        byte[] mixedOriginal = Utf8BomCrLf("Unrelated first line.\r\nSecond line.\n");
        File.WriteAllBytes(Path.Combine(vault, "mixed.md"), mixedOriginal);

        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.FailedNotes);
        Assert.Equal(definitionOriginal, File.ReadAllBytes(definitionPath));
        Assert.Equal(tooLargeOriginal, File.ReadAllBytes(Path.Combine(vault, "big.md")));
        Assert.Equal(mixedOriginal, File.ReadAllBytes(Path.Combine(vault, "mixed.md")));
    }

    /// <summary>A note deleted by the D8 rewrite-loop test seam, between the read and the stamp check, is
    /// listed as changed during the rename rather than throwing - the rename that already succeeded is not
    /// reported back to the caller as an exception (8.5.i-b fix).</summary>
    [Fact]
    public async Task Rename_NoteDeletedDuringRewrite_ListedAsChanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "plan.md"), Utf8BomCrLf("Plan body.\r\n"));
        string linkerPath = Path.Combine(vault, "linker.md");
        File.WriteAllBytes(linkerPath, Utf8BomCrLf("See [[plan]] here.\r\n"));
        fixture.Reload();
        LibraryFileService service = fixture.CreateService(beforeRewriteWriteForTests: _ =>
        {
            File.Delete(linkerPath);
            return Task.CompletedTask;
        });
        LibraryPath plan = fixture.Resolve(vault, "plan.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(plan, "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "roadmap.md")));
        LibraryNoteFailure failure = Assert.Single(result.Value.FailedNotes);
        Assert.Equal("linker.md", failure.RelativePath);
        Assert.Equal("linker.md changed during the rename; update its links by hand.", failure.Error);
    }
}
