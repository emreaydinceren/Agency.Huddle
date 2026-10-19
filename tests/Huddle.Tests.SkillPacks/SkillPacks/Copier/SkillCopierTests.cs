using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Resolving;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.SkillPacks.Copier;

/// <summary>Pins <see cref="SkillCopier"/> (§6.6, §8.6): the outcomes, the swap, the exact failure messages, what is not
/// copied, and <c>EnsureAvailableAsync</c>.</summary>
public sealed class SkillCopierTests
{
    /// <summary>A host whose one Pack points at a downloaded <c>docx</c>, read, with a clock far enough ahead that every file is quiet.</summary>
    private static async Task<(RegistryHost Host, SkillCopier Copier, string Pointer)> Ready(IFileReader? sourceReader = null)
    {
        RegistryHost host = new();
        host.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(1));
        host.Source();
        string pointer = host.CloneSkill("docx");
        host.Pack("documents", "docx", pointer);
        host.SkillsMd(["packs/documents.skillpack.json"]);
        await host.Registry.ReloadAsync(TestContext.Current.CancellationToken);
        SkillCopier copier = new(host.Registry, host.Settings, host.Log, NullLogger<SkillCopier>.Instance, host.Clock, sourceReader);
        return (host, copier, pointer);
    }

    /// <summary>The current entry for a Skill.</summary>
    private static SkillEntry Entry(RegistryHost host, string name)
    {
        return host.Registry.Current.Resolved.FindSkill(name) ?? throw new InvalidOperationException($"{name} is not in the registry");
    }

    /// <summary>A first copy is Created, with a new-format <c>.source.json</c> and a <c>copied</c> log line.</summary>
    [Fact]
    public async Task CopyAsync_FirstCopy_Created()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, string pointer) = await Ready();
        using RegistryHost owned = host;

        CopyResult result = await copier.CopyAsync([Entry(host, "docx")], ct);

        CopiedSkill copied = Assert.Single(result.Copied);
        Assert.True(result.Ok);
        Assert.Equal(CopyOutcome.Created, copied.Outcome);
        string target = Path.Combine(host.Settings.SkillsDir, "docx");
        Assert.True(SkillProvenance.TryRead(target, out SkillProvenance? provenance));
        Assert.Equal(1, provenance?.SchemaVersion);
        Assert.Equal("local/test", provenance?.Source);
        Assert.Equal(pointer, provenance?.CopiedFrom);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", provenance?.Commit);
        Assert.Contains("\"event\":\"copied\"", File.ReadAllText(Path.Combine(host.Dir, "sources", "local", "test", "log.jsonl")), StringComparison.Ordinal);
    }

    /// <summary>Copying again with nothing changed is AlreadyCurrent and writes nothing.</summary>
    [Fact]
    public async Task CopyAsync_Twice_AlreadyCurrent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, _) = await Ready();
        using RegistryHost owned = host;
        await copier.CopyAsync([Entry(host, "docx")], ct);
        DateTime written = File.GetLastWriteTimeUtc(Path.Combine(host.Settings.SkillsDir, "docx", ".source.json"));

        CopyResult second = await copier.CopyAsync([Entry(host, "docx")], ct);

        Assert.Equal(CopyOutcome.AlreadyCurrent, Assert.Single(second.Copied).Outcome);
        Assert.Equal(written, File.GetLastWriteTimeUtc(Path.Combine(host.Settings.SkillsDir, "docx", ".source.json")));
    }

    /// <summary>New files at the pointer are swapped in (Replaced) and the trash is emptied.</summary>
    [Fact]
    public async Task CopyAsync_PointerChanged_ReplacedAndTrashEmptied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, string pointer) = await Ready();
        using RegistryHost owned = host;
        await copier.CopyAsync([Entry(host, "docx")], ct);
        File.WriteAllText(Path.Combine(pointer, "extra.md"), "More.");
        await host.Registry.RebuildAsync(RebuildScope.All, ct);

        CopyResult result = await copier.CopyAsync([Entry(host, "docx")], ct);

        Assert.Equal(CopyOutcome.Replaced, Assert.Single(result.Copied).Outcome);
        Assert.True(File.Exists(Path.Combine(host.Settings.SkillsDir, "docx", "extra.md")));
        string trash = Path.Combine(host.Settings.SkillsDir, SkillCopyFolders.Trash);
        Assert.True(!Directory.Exists(trash) || Directory.GetFileSystemEntries(trash).Length == 0);
    }

    /// <summary>A folder the Human wrote, with no <c>.source.json</c>, is never replaced.</summary>
    [Fact]
    public async Task CopyAsync_HumansFolder_NameTaken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, _) = await Ready();
        using RegistryHost owned = host;
        RegistryHost.Write(Path.Combine(host.Settings.SkillsDir, "docx"), "SKILL.md", "---\nname: docx\ndescription: Mine.\n---\n");

        CopyResult result = await copier.CopyAsync([Entry(host, "docx")], ct);

        CopyFailure failure = Assert.Single(result.Failed);
        Assert.Equal(CopyFailureKind.NameTaken, failure.Kind);
        Assert.Equal("A different Skill called docx is already in Skills/, so Huddle did not replace it.", failure.Message);
        Assert.Equal("---\nname: docx\ndescription: Mine.\n---\n", File.ReadAllText(Path.Combine(host.Settings.SkillsDir, "docx", "SKILL.md")));
    }

    /// <summary>A copy whose files now grant an App Tool fails S011 on the copy, and nothing reaches Skills/.</summary>
    [Fact]
    public async Task CopyAsync_CopyGrantsAppTool_CheckFailedS011()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, string pointer) = await Ready();
        using RegistryHost owned = host;
        File.WriteAllText(Path.Combine(pointer, "SKILL.md"), "---\nname: docx\ndescription: Does docx things.\nlicense: MIT\ntools: propose_sprint\n---\nBody.\n");

        CopyResult result = await copier.CopyAsync([Entry(host, "docx")], ct);

        CopyFailure failure = Assert.Single(result.Failed);
        Assert.Equal(CopyFailureKind.CheckFailed, failure.Kind);
        Assert.StartsWith("docx failed S011 after copying:", failure.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(host.Settings.SkillsDir, "docx")));
    }

    /// <summary><c>.git</c> and <c>.source.json</c> at the pointer are not copied.</summary>
    [Fact]
    public async Task CopyAsync_GitAndSourceJson_NotCopied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, string pointer) = await Ready();
        using RegistryHost owned = host;
        RegistryHost.Write(pointer, ".git/HEAD", "ref: main");
        RegistryHost.Write(pointer, ".source.json", "{ \"stale\": true }");

        await copier.CopyAsync([Entry(host, "docx")], ct);

        string target = Path.Combine(host.Settings.SkillsDir, "docx");
        Assert.False(Directory.Exists(Path.Combine(target, ".git")));
        Assert.DoesNotContain("stale", File.ReadAllText(Path.Combine(target, ".source.json")), StringComparison.Ordinal);
    }

    /// <summary>A Skill that is not usable fails with its state; the next Skill in the batch is still copied.</summary>
    [Fact]
    public async Task CopyAsync_OneFailure_OthersStillCopied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, _) = await Ready();
        using RegistryHost owned = host;
        SkillEntry missing = Entry(host, "docx") with { Name = "ghost", FolderPath = null, PathState = SkillPathState.Empty };

        CopyResult result = await copier.CopyAsync([missing, Entry(host, "docx")], ct);

        Assert.Equal(CopyFailureKind.NotUsable, Assert.Single(result.Failed).Kind);
        Assert.Equal("docx", Assert.Single(result.Copied).Name);
    }

    /// <summary>A pointer that changes on every look fails Unstable after three attempts, with the exact message.</summary>
    [Fact]
    public async Task CopyAsync_KeepsChanging_UnstableAfterThreeAttempts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ChangingReader reader = new();
        (RegistryHost host, SkillCopier copier, _) = await Ready(reader);
        using RegistryHost owned = host;

        CopyResult result = await copier.CopyAsync([Entry(host, "docx")], ct);

        CopyFailure failure = Assert.Single(result.Failed);
        Assert.Equal(CopyFailureKind.Unstable, failure.Kind);
        Assert.Equal("The files for docx kept changing while Huddle copied them. Try again when your AI tool has finished.", failure.Message);
        Assert.Empty(Directory.GetDirectories(Path.Combine(host.Settings.SkillsDir, SkillCopyFolders.Staging)));
    }

    /// <summary>A cancelled copy throws and leaves no staging folder.</summary>
    [Fact]
    public async Task CopyAsync_Cancelled_RemovesStaging()
    {
        (RegistryHost host, SkillCopier copier, _) = await Ready();
        using RegistryHost owned = host;
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => copier.CopyAsync([Entry(host, "docx")], cancelled.Token));

        string staging = Path.Combine(host.Settings.SkillsDir, SkillCopyFolders.Staging);
        Assert.True(!Directory.Exists(staging) || Directory.GetDirectories(staging).Length == 0);
    }

    /// <summary>Ensure: a built-in name is skipped, a copyable one copied once, an unknown one reported; nothing throws.</summary>
    [Fact]
    public async Task EnsureAvailableAsync_SkipsCopiesAndReports()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, _) = await Ready();
        using RegistryHost owned = host;

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["team-building", "docx", "docx", "no-such"], ct);

        SkillSetupItem problem = Assert.Single(problems);
        Assert.Equal("no-such", problem.Name);
        Assert.Equal(SkillAvailability.Unknown, problem.Problem);
        Assert.True(Directory.Exists(Path.Combine(host.Settings.SkillsDir, "docx")));
        Assert.False(Directory.Exists(Path.Combine(host.Settings.SkillsDir, "team-building")));
    }

    /// <summary>Ensure never replaces a copy that exists, even when the pointer has an update.</summary>
    [Fact]
    public async Task EnsureAvailableAsync_UpdateAvailable_NotReplaced()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (RegistryHost host, SkillCopier copier, string pointer) = await Ready();
        using RegistryHost owned = host;
        await copier.CopyAsync([Entry(host, "docx")], ct);
        await Eventually.UntilAsync(() => host.Skills.Get("docx") is not null, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        File.WriteAllText(Path.Combine(pointer, "extra.md"), "More.");
        await host.Registry.RebuildAsync(RebuildScope.All, ct);

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["docx"], ct);

        Assert.Empty(problems);
        Assert.False(File.Exists(Path.Combine(host.Settings.SkillsDir, "docx", "extra.md")));
    }

    /// <summary>A <c>.trash</c> copy whose folder is missing is put back at start-up; then both folders are emptied.</summary>
    [Fact]
    public void Repair_TrashHoldsSkillAndFolderMissing_RestoresItBeforeSweep()
    {
        using TempDataDir dir = new();
        string skills = Path.Combine(dir.Path, "Skills");
        RegistryHost.Write(Path.Combine(skills, SkillCopyFolders.Trash, "docx-0a1b"), "SKILL.md", "kept");
        RegistryHost.Write(Path.Combine(skills, SkillCopyFolders.Trash, "pdf-9z"), "SKILL.md", "old");
        RegistryHost.Write(Path.Combine(skills, "pdf"), "SKILL.md", "current");
        RegistryHost.Write(Path.Combine(skills, SkillCopyFolders.Staging, "xlsx-1"), "SKILL.md", "half");

        IReadOnlyList<string> restored = SkillCopyFolders.Repair(skills, NullLogger.Instance);

        Assert.Equal(["docx"], restored);
        Assert.Equal("kept", File.ReadAllText(Path.Combine(skills, "docx", "SKILL.md")));
        Assert.Equal("current", File.ReadAllText(Path.Combine(skills, "pdf", "SKILL.md")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(skills, SkillCopyFolders.Trash)));
        Assert.Empty(Directory.GetDirectories(Path.Combine(skills, SkillCopyFolders.Staging)));
    }

    /// <summary>A disk reader whose answer for SKILL.md changes every time it is read, as if an AI tool kept writing.</summary>
    private sealed class ChangingReader : IFileReader
    {
        private int reads;

        /// <inheritdoc/>
        public bool FileExists(string path) => PhysicalFileReader.Instance.FileExists(path);

        /// <inheritdoc/>
        public bool DirectoryExists(string path) => PhysicalFileReader.Instance.DirectoryExists(path);

        /// <inheritdoc/>
        public string ReadAllText(string path) => PhysicalFileReader.Instance.ReadAllText(path);

        /// <inheritdoc/>
        public byte[] ReadAllBytes(string path)
        {
            byte[] bytes = PhysicalFileReader.Instance.ReadAllBytes(path);
            return path.EndsWith("SKILL.md", StringComparison.Ordinal) ? [.. bytes, (byte)Interlocked.Increment(ref this.reads)] : bytes;
        }

        /// <inheritdoc/>
        public long Length(string path) => PhysicalFileReader.Instance.Length(path);

        /// <inheritdoc/>
        public DateTimeOffset LastWriteTime(string path) => PhysicalFileReader.Instance.LastWriteTime(path);

        /// <inheritdoc/>
        public IReadOnlyList<string> EnumerateFiles(string directory, bool recursive) => PhysicalFileReader.Instance.EnumerateFiles(directory, recursive);

        /// <inheritdoc/>
        public IReadOnlyList<string> EnumerateDirectories(string directory) => PhysicalFileReader.Instance.EnumerateDirectories(directory);

        /// <inheritdoc/>
        public FileAttributes Attributes(string path) => PhysicalFileReader.Instance.Attributes(path);
    }
}
