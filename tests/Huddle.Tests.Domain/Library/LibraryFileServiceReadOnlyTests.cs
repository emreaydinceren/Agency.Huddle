using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the refusals <see cref="LibraryFileService"/> gives a read-only path (Skill Packs spec §6.11.3): every
/// writer returns <see cref="LibraryPath.ReadOnlyReason"/> and leaves the Skill Sources folder byte for byte as it was,
/// a rename elsewhere never rewrites a link inside it, and reading and searching still work.</summary>
public sealed class LibraryFileServiceReadOnlyTests : IDisposable
{
    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <summary>Writes the Skill Sources folder: SKILLS.md and one Pack file.</summary>
    public LibraryFileServiceReadOnlyTests()
    {
        Directory.CreateDirectory(Path.Combine(this.Folder, "packs"));
        File.WriteAllText(Path.Combine(this.Folder, "SKILLS.md"), "# Skills\n\nThe brief list.\n");
        File.WriteAllText(Path.Combine(this.Folder, "packs", "writing.skillpack.json"), "{}");
        Directory.CreateDirectory(Path.Combine(this.fixture.DataDir, "Teams", "Marketing"));
        File.WriteAllText(Path.Combine(this.fixture.DataDir, "Teams", "Marketing", "plan.md"), "plan\n");
    }

    /// <summary>The Skill Sources folder.</summary>
    private string Folder => Path.Combine(this.fixture.DataDir, "SkillSources");

    /// <summary>Deletes the temp data folder.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Saving SKILLS.md from the editor is refused and the file is unchanged.</summary>
    [Fact]
    public async Task WriteTextAsync_ReadOnly_RefusedAndUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        LibraryPath file = this.SkillSources("SKILLS.md");
        LibraryDocumentContent content = await service.ReadAsync(file, ct);
        Assert.NotNull(content.Format);
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(file, "changed", content.Format, ct);

        this.AssertRefusedAndUnchanged(result.Value, result.Error, before);
    }

    /// <summary>A new note and a new folder in the root or a sub-folder are refused, and nothing is created.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("packs")]
    public async Task CreateFileAndFolderAsync_ReadOnly_RefusedAndNothingCreated(string folderPath)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        LibraryPath folder = this.SkillSources(folderPath);
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();

        LibraryResult<LibraryPath> file = await service.CreateFileAsync(folder, "new.md", ct);
        LibraryResult<LibraryPath> created = await service.CreateFolderAsync(folder, "new", ct);

        this.AssertRefusedAndUnchanged(file.Value, file.Error, before);
        this.AssertRefusedAndUnchanged(created.Value, created.Error, before);
        Assert.False(Directory.Exists(Path.Combine(folder.FullPath, "new")));
    }

    /// <summary>An upload into the folder is refused and no file appears.</summary>
    [Fact]
    public async Task CreateFromStreamAsync_ReadOnly_RefusedAndNothingCreated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();
        using MemoryStream stream = new([1, 2, 3]);

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(this.SkillSources(string.Empty), "notes.txt", stream, 1024, ct);

        this.AssertRefusedAndUnchanged(result.Value, result.Error, before);
    }

    /// <summary>Deleting an "unsent upload" that is a file in the folder does nothing.</summary>
    [Fact]
    public async Task DeleteUnsentUploadAsync_ReadOnly_LeavesTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();

        await service.DeleteUnsentUploadAsync(this.SkillSources("SKILLS.md"), ct);

        Assert.Equal(before, this.Snapshot());
    }

    /// <summary>Renaming a file or folder in the folder is refused.</summary>
    [Theory]
    [InlineData("SKILLS.md", "Other.md")]
    [InlineData("packs", "bundles")]
    public async Task RenameAsync_ReadOnly_RefusedAndUnchanged(string item, string newName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(this.SkillSources(item), newName, ct);

        this.AssertRefusedAndUnchanged(result.Value, result.Error, before);
    }

    /// <summary>Moving a file out of the folder is refused, and so is moving a Teams note into it.</summary>
    [Fact]
    public async Task MoveAsync_OutOfOrIntoTheFolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();
        LibraryPath plan = this.fixture.ResolveTeams("Marketing/plan.md");

        LibraryResult<LibraryMoveResult> outOf = await service.MoveAsync(this.SkillSources("SKILLS.md"), this.fixture.ResolveTeams("Marketing"), ct);
        LibraryResult<LibraryMoveResult> into = await service.MoveAsync(plan, this.SkillSources("packs"), ct);

        this.AssertRefusedAndUnchanged(outOf.Value, outOf.Error, before);
        this.AssertRefusedAndUnchanged(into.Value, into.Error, before);
        Assert.True(File.Exists(plan.FullPath));
        Assert.False(File.Exists(Path.Combine(this.fixture.DataDir, "Teams", "Marketing", "SKILLS.md")));
    }

    /// <summary>Deleting a file or folder in the folder is refused and nothing goes to the recycle bin.</summary>
    [Theory]
    [InlineData("SKILLS.md")]
    [InlineData("packs")]
    public async Task RecycleAsync_ReadOnly_RefusedAndNothingRecycled(string item)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();
        IReadOnlyDictionary<string, byte[]> before = this.Snapshot();

        LibraryResult<LibraryPath> result = await service.RecycleAsync(this.SkillSources(item), ct);

        this.AssertRefusedAndUnchanged(result.Value, result.Error, before);
        Assert.Empty(this.fixture.RecycleBin.Sent);
    }

    /// <summary>SKILLS.md reads, not editable, with the reason; a search finds it.</summary>
    [Fact]
    public async Task ReadAndFind_ReadOnly_StillWork()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileService service = this.fixture.CreateService();

        LibraryDocumentContent content = await service.ReadAsync(this.SkillSources("SKILLS.md"), ct);
        LibrarySearchResult found = await service.FindAsync(this.SkillSources(string.Empty), "SKILLS", 50, ct);

        Assert.Equal("# Skills\n\nThe brief list.\n", content.Text);
        Assert.False(content.Editable);
        Assert.Contains(found.Hits, hit => hit.Path.RelativePath == "SKILLS.md");
    }

    /// <summary>Renaming a note in a pinned root rewrites the links to it there, but not the link in SKILLS.md inside the
    /// overlapping Skill Sources folder.</summary>
    [Fact]
    public async Task RenameAsync_Elsewhere_DoesNotRewriteSkillsMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture overlap = LibraryFileServiceFixture.Build(options => options.SkillSourcesDir = Path.Combine(options.DataDir, "Vault", "SkillSources"));
        string skillSources = Path.Combine(overlap.DataDir, "Vault", "SkillSources");
        Directory.CreateDirectory(skillSources);
        string vault = overlap.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "plan.md"), "plan\n");
        File.WriteAllText(Path.Combine(vault, "a.md"), "See [[plan]].\n");
        File.WriteAllText(Path.Combine(skillSources, "SKILLS.md"), "See [[plan]].\n");
        overlap.Reload();
        LibraryFileService service = overlap.CreateService();

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(overlap.Resolve(vault, "plan.md"), "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Equal("See [[roadmap]].\n", File.ReadAllText(Path.Combine(vault, "a.md")));
        Assert.Equal("See [[plan]].\n", File.ReadAllText(Path.Combine(skillSources, "SKILLS.md")));
        Assert.DoesNotContain(result.Value.RewrittenNotes, note => note.Contains("SKILLS", StringComparison.Ordinal));
    }

    /// <summary>Resolves a path in the Skill Sources root.</summary>
    private LibraryPath SkillSources(string relativePath)
    {
        Assert.True(this.fixture.Resolver.TryResolve(LibraryRootStore.SkillSourcesRootId, relativePath, out LibraryPath? path, out _));
        Assert.True(path.ReadOnly);
        return path;
    }

    /// <summary>Every file and folder under the Skill Sources folder, folders with no bytes.</summary>
    private SortedDictionary<string, byte[]> Snapshot()
    {
        SortedDictionary<string, byte[]> tree = new(StringComparer.Ordinal);
        foreach (string entry in Directory.GetFileSystemEntries(this.Folder, "*", SearchOption.AllDirectories))
        {
            tree[Path.GetRelativePath(this.Folder, entry)] = File.Exists(entry) ? File.ReadAllBytes(entry) : [];
        }

        return tree;
    }

    /// <summary>The call returned the read-only refusal and the folder is as it was.</summary>
    private void AssertRefusedAndUnchanged(object? value, string? error, IReadOnlyDictionary<string, byte[]> before)
    {
        Assert.Null(value);
        Assert.Equal(LibraryPath.ReadOnlyReason, error);
        Assert.Equal(before, this.Snapshot());
    }
}
