using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.SkillPacks.RegistryService;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.SkillPacks.Copier;

/// <summary>Pins <see cref="SkillCopier.EnsureAvailableAsync"/> against a real registry (Skill Packs spec §6.10.3): what is
/// skipped, what is copied and how often, and that every not-ready state comes back as a problem and writes nothing.</summary>
public sealed class SkillCopierEnsureTests
{
    /// <summary>A built-in name is skipped and nothing is written for it.</summary>
    [Fact]
    public async Task Ensure_BuiltIn_Skipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        SkillCopier copier = await ReadyAsync(host, ct);

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["team-building"], ct);

        Assert.Empty(problems);
        Assert.False(Directory.Exists(Path.Combine(host.Settings.SkillsDir, "team-building")));
    }

    /// <summary>A copyable name asked for twice is copied once: one <c>copied</c> line in the Source's log.</summary>
    [Fact]
    public async Task Ensure_Copyable_CopiedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        SkillCopier copier = await ReadyAsync(host, ct);

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["docx", "docx"], ct);

        Assert.Empty(problems);
        Assert.True(File.Exists(Path.Combine(host.Settings.SkillsDir, "docx", "SKILL.md")));
        Assert.Single(CopiedLines(host));
    }

    /// <summary>A name already in Skills/ is not copied again: a file added to the copy survives and no second line is logged.</summary>
    [Fact]
    public async Task Ensure_Present_NotRecopied()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        SkillCopier copier = await ReadyAsync(host, ct);
        _ = await copier.EnsureAvailableAsync(["docx"], ct);
        await Eventually.UntilAsync(() => host.Skills.Get("docx") is not null, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        string sentinel = Path.Combine(host.Settings.SkillsDir, "docx", "sentinel.txt");
        File.WriteAllText(sentinel, "kept");

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["docx"], ct);

        Assert.Empty(problems);
        Assert.True(File.Exists(sentinel));
        Assert.Single(CopiedLines(host));
    }

    /// <summary>A not-downloaded, a broken and an unknown name each come back as that problem, and none is written.</summary>
    [Fact]
    public async Task Ensure_NotDownloadedBrokenUnknown_ReportedAndNothingWritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        host.Source();
        string broken = Path.Combine(host.Clone, "skills", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, "README.md"), "No SKILL.md here.");
        host.Pack("lost", "ghost", null);
        host.Pack("bad", "broken", broken);
        host.SkillsMd(["packs/lost.skillpack.json", "packs/bad.skillpack.json"]);
        SkillCopier copier = await CopierAsync(host, ct);

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["ghost", "broken", "no-such"], ct);

        Assert.Equal(
            [("ghost", SkillAvailability.NotDownloaded), ("broken", SkillAvailability.Broken), ("no-such", SkillAvailability.Unknown)],
            problems.Select(static item => (item.Name, item.Problem)));
        Assert.False(Directory.Exists(host.Settings.SkillsDir) && Directory.EnumerateFileSystemEntries(host.Settings.SkillsDir).Any(entry => !Path.GetFileName(entry).StartsWith('.')));
    }

    /// <summary>A Skill its Source withdrew comes back as Withdrawn with the Source's reason, and is not written.</summary>
    [Fact]
    public async Task Ensure_Withdrawn_ReportedWithReasonAndNothingWritten()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RegistryHost host = new();
        host.Source("withdrawn");
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        SkillCopier copier = await CopierAsync(host, ct);

        IReadOnlyList<SkillSetupItem> problems = await copier.EnsureAvailableAsync(["docx"], ct);

        SkillSetupItem problem = Assert.Single(problems);
        Assert.Equal(SkillAvailability.Withdrawn, problem.Problem);
        Assert.Equal("Because.", problem.Detail);
        Assert.False(Directory.Exists(Path.Combine(host.Settings.SkillsDir, "docx")));
    }

    /// <summary>One Pack pointing at a downloaded <c>docx</c>, read, and a copier over it.</summary>
    private static Task<SkillCopier> ReadyAsync(RegistryHost host, CancellationToken ct)
    {
        host.Source();
        host.Pack("documents", "docx", host.CloneSkill("docx"));
        host.SkillsMd(["packs/documents.skillpack.json"]);
        return CopierAsync(host, ct);
    }

    /// <summary>Reads the registry with the clock far enough ahead that every file is quiet, and builds the copier.</summary>
    private static async Task<SkillCopier> CopierAsync(RegistryHost host, CancellationToken ct)
    {
        host.Clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(1));
        await host.Registry.ReloadAsync(ct);
        return new SkillCopier(host.Registry, host.Settings, host.Log, NullLogger<SkillCopier>.Instance, host.Clock);
    }

    /// <summary>The <c>copied</c> lines in the test Source's log.</summary>
    private static IReadOnlyList<string> CopiedLines(RegistryHost host)
    {
        string log = Path.Combine(host.Settings.Dir, "sources", "local", "test", SourceLog.FileName);
        return File.Exists(log) ? [.. File.ReadAllLines(log).Where(static line => line.Contains("\"copied\"", StringComparison.Ordinal))] : [];
    }

}
