using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Memory;

/// <summary>
/// Pins <see cref="MemoryWriter.ReplaceAndRemoveAsync"/> and <see cref="MemoryWriter.PruneReportedAsync"/>, the two methods a Memory
/// Tidy pass applies a step with (Memory Tidy spec §6.11, §6.6).
/// </summary>
public sealed class MemoryWriterTidyTests : IDisposable
{
    private const string Description = "Interview recording rules; read before scheduling an interview";
    private const string NewDescription = "Recording consent rules for every interview we run";

    private readonly TempDataDir dir = new();
    private readonly MemoryFolderGate gate = new();
    private readonly MemoryWriter writer;
    private readonly MemoryFolder folder;
    private readonly string backup;

    /// <summary>Builds a writer over a temp folder, with a size cap of 400 bytes.</summary>
    public MemoryWriterTidyTests()
    {
        IOptions<TeamOptions> options = this.dir.Options(static team => team.Memory.MaxFileBytes = 400);
        this.writer = new MemoryWriter(this.gate, options);
        this.folder = new MemoryFolder(Path.Combine(this.dir.Path, "memory"), "me", null);
        this.backup = Path.Combine(this.dir.Path, "backups", "2026-11-16", "Agents", "Nova");
    }

    /// <summary>Releases the folder gate and removes the temp directory.</summary>
    public void Dispose()
    {
        this.gate.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A rewrite writes the new body and description, keeps the other keys, and leaves the old bytes in the backup.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_Rewrite_WritesTheFileAndBacksUpTheOldBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Old body.", "owner: Ana\n"), ct);
        byte[] before = await File.ReadAllBytesAsync(this.PathOf("consent.md"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, "New body.", version), []), this.backup, ct);

        MemoryTidyResult.Done done = Assert.IsType<MemoryTidyResult.Done>(result);
        string after = await File.ReadAllTextAsync(this.PathOf("consent.md"), ct);
        Assert.Equal(MemoryFileRules.Compose(NewDescription, "New body.", "owner: Ana\n"), after);
        Assert.Equal(MemoryVersion.Of(await File.ReadAllBytesAsync(this.PathOf("consent.md"), ct)), done.WrittenVersion);
        Assert.Equal(Path.Combine(this.backup, "consent.md"), Assert.Single(done.Copies));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(this.backup, "consent.md"), ct));
        Assert.Empty(done.LeftBehind);
    }

    /// <summary>A describe changes only the description: the body stays byte for byte, even without a trailing line break.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_Describe_KeepsTheBodyBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string body = "Keep this body\n\nexactly   as is";
        string version = await this.SeedAsync("consent.md", $"---\ndescription: {Description}\nowner: Ana\n---\n\n{body}", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, null, version), []), this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal($"---\ndescription: {NewDescription}\nowner: Ana\n---\n\n{body}", await File.ReadAllTextAsync(this.PathOf("consent.md"), ct));
    }

    /// <summary>A merge writes the kept file and removes the others, and every one of them is in the backup as it was.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_Merge_WritesIntoAndRemovesTheRest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string into = await this.SeedAsync("into.md", MemoryFileRules.Compose(Description, "Into body.\n"), ct);
        string fromA = await this.SeedAsync("from-a.md", MemoryFileRules.Compose(Description, "From a.\n"), ct);
        string fromB = await this.SeedAsync("from-b.md", MemoryFileRules.Compose(Description, "From b.\n"), ct);
        Dictionary<string, byte[]> before = [];
        foreach (string name in new[] { "into.md", "from-a.md", "from-b.md" })
        {
            before[name] = await File.ReadAllBytesAsync(this.PathOf(name), ct);
        }

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder,
            new MemoryTidyChange(
                new MemoryTidyWrite("into.md", NewDescription, "Into body.\nFrom a.\nFrom b.\n", into),
                [new MemoryTidyRemoval("from-a.md", fromA), new MemoryTidyRemoval("from-b.md", fromB)]),
            this.backup,
            ct);

        MemoryTidyResult.Done done = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal(3, done.Copies.Count);
        Assert.False(File.Exists(this.PathOf("from-a.md")));
        Assert.False(File.Exists(this.PathOf("from-b.md")));
        Assert.Contains("From b.", await File.ReadAllTextAsync(this.PathOf("into.md"), ct), StringComparison.Ordinal);
        foreach ((string name, byte[] bytes) in before)
        {
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(this.backup, name), ct));
        }
    }

    /// <summary>A drop removes the file, writes nothing, and keeps the file in the backup.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_Drop_RemovesOnlyAndKeepsACopy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("old.md", MemoryFileRules.Compose(Description, "Old.\n"), ct);
        _ = await this.SeedAsync("keep.md", MemoryFileRules.Compose(Description, "Keep.\n"), ct);
        byte[] before = await File.ReadAllBytesAsync(this.PathOf("old.md"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(null, [new MemoryTidyRemoval("old.md", version)]), this.backup, ct);

        MemoryTidyResult.Done done = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Null(done.WrittenVersion);
        Assert.False(File.Exists(this.PathOf("old.md")));
        Assert.True(File.Exists(this.PathOf("keep.md")));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(this.backup, "old.md"), ct));
    }

    /// <summary>The copy holds the bytes that were version-checked, even when the file is replaced on disk between the read and the copy.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_FileReplacedAfterTheRead_CopyHoldsTheCheckedBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Checked body.\n"), ct);
        byte[] checkedBytes = await File.ReadAllBytesAsync(this.PathOf("consent.md"), ct);
        this.writer.AfterReadForTests = _ => File.WriteAllTextAsync(this.PathOf("consent.md"), "a Library save with another text", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, "New.", version), []), this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal(checkedBytes, await File.ReadAllBytesAsync(Path.Combine(this.backup, "consent.md"), ct));
    }

    /// <summary>The copy is made before the write: a write that then fails still leaves the file's old bytes in the backup.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_WriteFailsAfterTheCopy_ReturnsFailedWithTheCopyKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Checked body.\n"), ct);
        byte[] checkedBytes = await File.ReadAllBytesAsync(this.PathOf("consent.md"), ct);
        this.writer.AfterReadForTests = _ => this.ReplaceWithAFolderAsync("consent.md");

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, "New.", version), []), this.backup, ct);

        Assert.Equal("consent.md", Assert.IsType<MemoryTidyResult.Failed>(result).File);
        Assert.Equal(checkedBytes, await File.ReadAllBytesAsync(Path.Combine(this.backup, "consent.md"), ct));
    }

    /// <summary>A copy already in the backup is never overwritten, and still counts as the file's copy.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_CopyAlreadyThere_LeavesItAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Second state.\n"), ct);
        Directory.CreateDirectory(this.backup);
        await File.WriteAllTextAsync(Path.Combine(this.backup, "consent.md"), "the first copy of this pass", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, "Third.", version), []), this.backup, ct);

        MemoryTidyResult.Done done = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Single(done.Copies);
        Assert.Equal("the first copy of this pass", await File.ReadAllTextAsync(Path.Combine(this.backup, "consent.md"), ct));
    }

    /// <summary>A file where the backup folder should be gives CopyFailed and nothing has changed, removals included.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_BackupFolderIsAFile_ReturnsCopyFailedAndChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string into = await this.SeedAsync("into.md", MemoryFileRules.Compose(Description, "Into.\n"), ct);
        string from = await this.SeedAsync("from.md", MemoryFileRules.Compose(Description, "From.\n"), ct);
        string intoText = await File.ReadAllTextAsync(this.PathOf("into.md"), ct);
        Directory.CreateDirectory(Path.GetDirectoryName(this.backup)!);
        await File.WriteAllTextAsync(this.backup, "I am a file", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder,
            new MemoryTidyChange(new MemoryTidyWrite("into.md", NewDescription, "Merged.", into), [new MemoryTidyRemoval("from.md", from)]),
            this.backup,
            ct);

        _ = Assert.IsType<MemoryTidyResult.CopyFailed>(result);
        Assert.Equal(intoText, await File.ReadAllTextAsync(this.PathOf("into.md"), ct));
        Assert.True(File.Exists(this.PathOf("from.md")));
    }

    /// <summary>A write whose version is not the file's changes nothing and makes no copy.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_WriteVersionDiffers_ReturnsStale()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Body.\n"), ct);
        string before = await File.ReadAllTextAsync(this.PathOf("consent.md"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, "New.", "not-the-version"), []), this.backup, ct);

        Assert.Equal("consent.md", Assert.IsType<MemoryTidyResult.Stale>(result).File);
        Assert.Equal(before, await File.ReadAllTextAsync(this.PathOf("consent.md"), ct));
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>A removal whose version is not the file's changes nothing, the write included.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_RemovalVersionDiffers_ReturnsStaleAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string into = await this.SeedAsync("into.md", MemoryFileRules.Compose(Description, "Into.\n"), ct);
        _ = await this.SeedAsync("from.md", MemoryFileRules.Compose(Description, "From.\n"), ct);
        string intoText = await File.ReadAllTextAsync(this.PathOf("into.md"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder,
            new MemoryTidyChange(new MemoryTidyWrite("into.md", NewDescription, "Merged.", into), [new MemoryTidyRemoval("from.md", "old-version")]),
            this.backup,
            ct);

        Assert.Equal("from.md", Assert.IsType<MemoryTidyResult.Stale>(result).File);
        Assert.Equal(intoText, await File.ReadAllTextAsync(this.PathOf("into.md"), ct));
        Assert.True(File.Exists(this.PathOf("from.md")));
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>A named file that is gone is stale, and the rest of the step is not applied.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_NamedFileMissing_ReturnsStale()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string into = await this.SeedAsync("into.md", MemoryFileRules.Compose(Description, "Into.\n"), ct);
        string intoText = await File.ReadAllTextAsync(this.PathOf("into.md"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder,
            new MemoryTidyChange(new MemoryTidyWrite("into.md", NewDescription, "Merged.", into), [new MemoryTidyRemoval("gone.md", "any")]),
            this.backup,
            ct);

        Assert.Equal("gone.md", Assert.IsType<MemoryTidyResult.Stale>(result).File);
        Assert.Equal(intoText, await File.ReadAllTextAsync(this.PathOf("into.md"), ct));
    }

    /// <summary>The file's own other frontmatter keys survive a rewrite, and keys echoed at the top of the text are thrown away.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_EchoedFrontmatter_IsDiscardedAndTheFilesOwnKeysKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Old.\n", "owner: Ana\n"), ct);
        const string echoed = "---\ndescription: An echoed description of the file\nowner: Bob\nextra: 1\n---\n\nNew body";

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, echoed, version), []), this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal($"---\ndescription: {NewDescription}\nowner: Ana\n---\n\nNew body\n", await File.ReadAllTextAsync(this.PathOf("consent.md"), ct));
    }

    /// <summary>A null description keeps the file's own description lines exactly as they are on disk, a folded one included.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_NullDescription_KeepsTheLineAsOnDisk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", "---\ndescription: >\n  Folded line one of it\n  folded line two\nowner: Ana\n---\n\nOld.\n", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", null, "New body.", version), []), this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal(
            "---\ndescription: >\n  Folded line one of it\n  folded line two\nowner: Ana\n---\n\nNew body.\n",
            await File.ReadAllTextAsync(this.PathOf("consent.md"), ct));
    }

    /// <summary>A null description on a file that has none is refused as missing, and nothing changes.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_NullDescriptionAndNoneOnDisk_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("bare.md", "Just a body, no frontmatter.\n", ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("bare.md", null, "New.", version), []), this.backup, ct);

        Assert.Contains("missing", Assert.IsType<MemoryTidyResult.Refused>(result).Why, StringComparison.Ordinal);
        Assert.Equal("Just a body, no frontmatter.\n", await File.ReadAllTextAsync(this.PathOf("bare.md"), ct));
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>reported.md and MEMORY.md, in any case, are refused wherever the step names them, and nothing changes.</summary>
    [Theory]
    [InlineData("reported.md", true)]
    [InlineData("Reported.MD", true)]
    [InlineData("reported.md", false)]
    [InlineData("MEMORY.md", true)]
    [InlineData("memory.md", false)]
    public async Task ReplaceAndRemoveAsync_ReportedOrIndexFile_IsRefused(string name, bool asWrite)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("reported.md", MemoryFileRules.Compose(Description, "- 2026-11-01 · told them\n"), ct);
        string before = await File.ReadAllTextAsync(this.PathOf("reported.md"), ct);
        MemoryTidyChange change = asWrite
            ? new MemoryTidyChange(new MemoryTidyWrite(name, NewDescription, "New.", version), [])
            : new MemoryTidyChange(null, [new MemoryTidyRemoval(name, version)]);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(this.folder, change, this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Refused>(result);
        Assert.Equal(before, await File.ReadAllTextAsync(this.PathOf("reported.md"), ct));
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>A text over the size cap is refused with the size in the reason, and nothing changes.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_OverTheSizeCap_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Old.\n"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", NewDescription, new string('x', 600), version), []), this.backup, ct);

        Assert.Contains("400", Assert.IsType<MemoryTidyResult.Refused>(result).Why, StringComparison.Ordinal);
        Assert.Contains("Old.", await File.ReadAllTextAsync(this.PathOf("consent.md"), ct), StringComparison.Ordinal);
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>A description the rules refuse stops a rewrite, and nothing changes.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_DescriptionBreaksTheRule_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("consent.md", MemoryFileRules.Compose(Description, "Old.\n"), ct);

        MemoryTidyResult result = await this.writer.ReplaceAndRemoveAsync(
            this.folder, new MemoryTidyChange(new MemoryTidyWrite("consent.md", "too short", "New.", version), []), this.backup, ct);

        Assert.Contains("shorter", Assert.IsType<MemoryTidyResult.Refused>(result).Why, StringComparison.Ordinal);
        Assert.Contains("Old.", await File.ReadAllTextAsync(this.PathOf("consent.md"), ct), StringComparison.Ordinal);
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>Two calls on one folder do not interleave: the second does not start while the first holds the gate.</summary>
    [Fact]
    public async Task ReplaceAndRemoveAsync_TwoCallsOnOneFolder_DoNotInterleave()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string versionA = await this.SeedAsync("a.md", MemoryFileRules.Compose(Description, "A.\n"), ct);
        string versionB = await this.SeedAsync("b.md", MemoryFileRules.Compose(Description, "B.\n"), ct);
        int entered = 0;
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        this.writer.AfterReadForTests = async _ =>
        {
            if (Interlocked.Increment(ref entered) == 1)
            {
                firstEntered.SetResult();
                await release.Task.ConfigureAwait(false);
            }
        };

        Task<MemoryTidyResult> first = this.writer.ReplaceAndRemoveAsync(this.folder, new MemoryTidyChange(null, [new MemoryTidyRemoval("a.md", versionA)]), this.backup, ct);
        await firstEntered.Task.WaitAsync(ct);
        Task<MemoryTidyResult> second = this.writer.ReplaceAndRemoveAsync(this.folder, new MemoryTidyChange(null, [new MemoryTidyRemoval("b.md", versionB)]), this.backup, ct);
        for (int turn = 0; turn < 2000 && Volatile.Read(ref entered) < 2 && !second.IsCompleted; turn++)
        {
            await Task.Yield();
        }

        int enteredWhileHeld = Volatile.Read(ref entered);
        bool secondDoneWhileHeld = second.IsCompleted;
        release.SetResult();
        MemoryTidyResult[] results = await Task.WhenAll(first, second);

        Assert.Equal(1, enteredWhileHeld);
        Assert.False(secondDoneWhileHeld);
        Assert.All(results, static each => Assert.IsType<MemoryTidyResult.Done>(each));
        Assert.Equal(2, Volatile.Read(ref entered));
    }

    /// <summary>A prune changes only the body: the frontmatter is the same bytes, and the old file is in the backup.</summary>
    [Fact]
    public async Task PruneReportedAsync_ReplacesOnlyTheBody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string head = "---\ndescription: Things I told the Human directly, one dated line each\nowner: Ana\n---\n\n";
        string version = await this.SeedAsync("reported.md", head + "- 2026-10-01 · old\n- 2026-11-15 · new\n", ct);
        byte[] before = await File.ReadAllBytesAsync(this.PathOf("reported.md"), ct);

        MemoryTidyResult result = await this.writer.PruneReportedAsync(this.folder, "\n- 2026-11-15 · new\n", version, this.backup, ct);

        MemoryTidyResult.Done done = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal(head + "- 2026-11-15 · new\n", await File.ReadAllTextAsync(this.PathOf("reported.md"), ct));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(this.backup, "reported.md"), ct));
        Assert.Equal(MemoryVersion.Of(await File.ReadAllBytesAsync(this.PathOf("reported.md"), ct)), done.WrittenVersion);
    }

    /// <summary>A description the rules would refuse does not stop a prune.</summary>
    [Fact]
    public async Task PruneReportedAsync_BadDescription_StillPrunes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string head = "---\ndescription: x\n---\n\n";
        string version = await this.SeedAsync("reported.md", head + "- 2026-10-01 · old\n", ct);

        MemoryTidyResult result = await this.writer.PruneReportedAsync(this.folder, "\n", version, this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Done>(result);
        Assert.Equal(head, await File.ReadAllTextAsync(this.PathOf("reported.md"), ct));
    }

    /// <summary>The prune's copy is made first: a write that then fails still leaves the old bytes in the backup.</summary>
    [Fact]
    public async Task PruneReportedAsync_WriteFailsAfterTheCopy_ReturnsFailedWithTheCopyKept()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("reported.md", MemoryFileRules.Compose(Description, "- 2026-10-01 · old\n"), ct);
        byte[] before = await File.ReadAllBytesAsync(this.PathOf("reported.md"), ct);
        this.writer.AfterReadForTests = _ => this.ReplaceWithAFolderAsync("reported.md");

        MemoryTidyResult result = await this.writer.PruneReportedAsync(this.folder, string.Empty, version, this.backup, ct);

        _ = Assert.IsType<MemoryTidyResult.Failed>(result);
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(this.backup, "reported.md"), ct));
    }

    /// <summary>A stale version changes nothing and makes no copy.</summary>
    [Fact]
    public async Task PruneReportedAsync_VersionDiffers_ReturnsStale()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.SeedAsync("reported.md", MemoryFileRules.Compose(Description, "- 2026-10-01 · old\n"), ct);
        string before = await File.ReadAllTextAsync(this.PathOf("reported.md"), ct);

        MemoryTidyResult result = await this.writer.PruneReportedAsync(this.folder, string.Empty, "not-the-version", this.backup, ct);

        Assert.Equal("reported.md", Assert.IsType<MemoryTidyResult.Stale>(result).File);
        Assert.Equal(before, await File.ReadAllTextAsync(this.PathOf("reported.md"), ct));
        Assert.False(Directory.Exists(this.backup));
    }

    /// <summary>A prune takes no file name: it writes reported.md and leaves every other file in the folder alone.</summary>
    [Fact]
    public async Task PruneReportedAsync_TouchesOnlyReportedMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string version = await this.SeedAsync("reported.md", MemoryFileRules.Compose(Description, "- 2026-10-01 · old\n"), ct);
        _ = await this.SeedAsync("other.md", MemoryFileRules.Compose(Description, "- 2026-10-01 · old\n"), ct);
        string otherBefore = await File.ReadAllTextAsync(this.PathOf("other.md"), ct);

        _ = await this.writer.PruneReportedAsync(this.folder, string.Empty, version, this.backup, ct);

        Assert.Equal(otherBefore, await File.ReadAllTextAsync(this.PathOf("other.md"), ct));
        Assert.Equal(
            ["other.md", "reported.md"],
            Directory.GetFiles(this.folder.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(["reported.md"], Directory.GetFiles(this.backup).Select(Path.GetFileName).ToArray());
    }

    /// <summary>The path of a file in the memory folder.</summary>
    /// <param name="name">The file name.</param>
    private string PathOf(string name) => Path.Combine(this.folder.Path, name);

    /// <summary>Writes a file into the memory folder.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="text">The file's text.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The file's version.</returns>
    private async Task<string> SeedAsync(string name, string text, CancellationToken ct)
    {
        Directory.CreateDirectory(this.folder.Path);
        await File.WriteAllTextAsync(this.PathOf(name), text, ct);
        return MemoryVersion.Of(await File.ReadAllBytesAsync(this.PathOf(name), ct));
    }

    /// <summary>Deletes a file and puts a folder of the same name in its place, so the write that follows cannot land.</summary>
    /// <param name="name">The file name.</param>
    private Task ReplaceWithAFolderAsync(string name)
    {
        File.Delete(this.PathOf(name));
        Directory.CreateDirectory(this.PathOf(name));
        return Task.CompletedTask;
    }
}
