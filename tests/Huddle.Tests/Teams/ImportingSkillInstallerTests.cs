using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary><see cref="ImportingSkillInstaller"/> (HR-9): Start Team imports a missing skill from its owner, and stops on one that needs review.</summary>
public sealed class ImportingSkillInstallerTests
{
    private const string Tdd = "test-driven-development";

    private const string TddId = "addyosmani/agent-skills/test-driven-development";

    private static SkillRef Ref(string name, string source = "addyosmani/agent-skills", string? path = null) => new(name, source, path ?? $"skills/{name}");

    private static ReviewFlag Flag(ReviewFlagKind kind) => new(kind, "SKILL.md", 1, "x");

    private static ImportingSkillInstaller Create(TempDataDir dir, FakeSkillImporter importer, out SkillStore store)
    {
        store = new SkillStore(dir.Options(), NullLogger<SkillStore>.Instance);
        SkillsCatalogIndex catalog = new(dir.Options(), NullLogger<SkillsCatalogIndex>.Instance);
        return new ImportingSkillInstaller(store, catalog, importer, NullLogger<ImportingSkillInstaller>.Instance);
    }

    /// <summary>A preview whose name is not the skill asked for is never installed and the skill is reported missing (review fix item 7).</summary>
    [Fact]
    public async Task EnsureInstalledAsync_PreviewNameDiffers_NotInstalled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId[TddId] = FakeSkillImporter.MakePreview("some-other-skill");
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([Ref(Tdd)], ct);

            Assert.Equal(Tdd, Assert.Single(missing).Name);
            Assert.Empty(importer.Installs);
        }
    }

    /// <summary>A builtin skill and one the store already holds are not imported and are not reported missing.</summary>
    [Fact]
    public async Task EnsureInstalledAsync_BuiltinOrPresent_SkipsImport()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([new SkillRef("anything", "builtin", null), Ref("team-building")], ct);

            Assert.Empty(missing);
            Assert.Empty(importer.CatalogCalls);
            Assert.Empty(importer.Installs);
        }
    }

    /// <summary>A missing skill with only a license flag is previewed from the catalog and installed.</summary>
    [Fact]
    public async Task EnsureInstalledAsync_MissingCleanSkill_Installs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        ImportPreview preview = FakeSkillImporter.MakePreview(Tdd, flags: [Flag(ReviewFlagKind.NoLicense)]);
        importer.ByCatalogId[TddId] = preview;
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([Ref(Tdd)], ct);

            Assert.Empty(missing);
            Assert.Equal([TddId], importer.CatalogCalls);
            Assert.Same(preview, Assert.Single(importer.Installs));
        }
    }

    /// <summary>A skill with a flag other than the license one is returned for review and never installed.</summary>
    [Fact]
    public async Task EnsureInstalledAsync_FlaggedSkill_ReturnedNeedsReview()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId[TddId] = FakeSkillImporter.MakePreview(Tdd, flags: [Flag(ReviewFlagKind.NoLicense), Flag(ReviewFlagKind.Script)]);
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            SkillRef wanted = Ref(Tdd);

            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([wanted], ct);

            Assert.Equal([wanted], missing);
            Assert.Empty(importer.Installs);
        }
    }

    /// <summary>A failed install, an unavailable preview, an unlisted skill and a skill without a path are all returned.</summary>
    [Fact]
    public async Task EnsureInstalledAsync_FailuresAndUnknowns_ReturnedMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new() { Outcome = new ImportOutcome(false, "Download failed.", null) };
        importer.ByCatalogId[TddId] = FakeSkillImporter.MakePreview(Tdd);
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            SkillRef failing = Ref(Tdd);
            SkillRef noPreview = Ref("incremental-implementation");
            SkillRef unlisted = Ref("not-in-catalog");
            SkillRef noPath = new("code-review-and-quality", "addyosmani/agent-skills", null);

            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([failing, noPreview, unlisted, noPath], ct);

            Assert.Equal([failing, noPreview, unlisted, noPath], missing);
        }
    }

    /// <summary>A preview that cannot be installed is returned without calling install.</summary>
    [Fact]
    public async Task EnsureInstalledAsync_PreviewCannotInstall_ReturnedMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeSkillImporter importer = new();
        importer.ByCatalogId[TddId] = FakeSkillImporter.MakePreview(Tdd, canInstall: false);
        ImportingSkillInstaller installer = Create(dir, importer, out SkillStore store);
        using (store)
        {
            SkillRef wanted = Ref(Tdd);

            IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync([wanted], ct);

            Assert.Equal([wanted], missing);
            Assert.Empty(importer.Installs);
        }
    }
}
