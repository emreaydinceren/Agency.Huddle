using System.IO.Compression;
using System.Net;
using System.Text;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Desktop;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// <see cref="SkillImporter"/> (SK-2 to SK-7): the preview shows provenance, differences and flags, nothing is
/// written before <see cref="SkillImporter.Install"/>, and an install is idempotent and never overwrites a local skill.
/// </summary>
public sealed class SkillImporterTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private const string Repo = "https://github.com/acme/skills";

    private const string Catalog = """
        [
          { "id": "acme/skills/alpha", "name": "alpha", "description": "Alpha.", "publisher": "Acme",
            "repo": "https://github.com/acme/skills", "path": "skills/alpha",
            "link": "https://github.com/acme/skills/tree/0123456789abcdef0123456789abcdef01234567/skills/alpha",
            "commit": "0123456789abcdef0123456789abcdef01234567", "license": "MIT",
            "category": "Cat", "subCategory": "Sub", "requires": [] }
        ]
        """;

    private const string SkillText = "---\nname: alpha\ndescription: Alpha.\n---\nDo the alpha thing.\n";

    /// <summary>Builds a zip whose single top-level folder holds the given entries.</summary>
    private static byte[] BuildZip(string root, IEnumerable<(string Name, string Content)> entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using Stream entry = archive.CreateEntry(root + "/" + name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    /// <summary>The default archive: one valid skill under <c>skills/alpha</c>, plus any extra files.</summary>
    private static byte[] AlphaZip(string skillText = SkillText, params (string Name, string Content)[] extra)
    {
        List<(string Name, string Content)> entries = [("skills/alpha/SKILL.md", skillText), .. extra.Select(static e => ("skills/alpha/" + e.Name, e.Content))];
        return BuildZip("skills-" + Commit, entries);
    }

    /// <summary>Answers every request with a zip.</summary>
    private static Func<Uri, HttpResponseMessage> Serve(byte[] zip) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) };

    /// <summary>Builds a skill preview by hand for the install-only tests.</summary>
    private static ImportPreview PreviewOf(IReadOnlyDictionary<string, byte[]> files, bool canInstall = true) =>
        new(null, Repo, "skills/alpha", Commit, "Acme", "MIT", "alpha", "Alpha.", [], [], [], canInstall, new DownloadedSkill(files));

    /// <summary>Everything a test needs: a data folder, the stores, a scripted network and the importer.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();

        public Harness(Func<Uri, HttpResponseMessage>? respond = null)
        {
            File.WriteAllText(Path.Combine(this.dir.Path, "SkillsCatalog.json"), Catalog);
            this.Handler = new FakeHttpHandler((request, _) => (respond ?? this.Default)(request.RequestUri!));
            HttpClient http = new(this.Handler);
            this.Skills = new SkillStore(this.dir.Options(), NullLogger<SkillStore>.Instance);
            this.Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
            this.Importer = new SkillImporter(
                new SkillsCatalogIndex(this.dir.Options(), NullLogger<SkillsCatalogIndex>.Instance),
                new SkillDownloader(http, NullLogger<SkillDownloader>.Instance),
                this.Skills,
                http,
                this.Clock);
        }

        public FakeHttpHandler Handler { get; }

        public SkillStore Skills { get; }

        public FakeTimeProvider Clock { get; }

        public SkillImporter Importer { get; }

        public string SkillsDir => this.Skills.SkillsDirectory;

        /// <summary>The folders a failed or half-done import would leave next to the Skills folder.</summary>
        public string[] LeftoverTempFolders() =>
            Directory.GetDirectories(this.dir.Path, ".skill-import-*");

        /// <summary>Serves the default archive for codeload and a fixed commit for the GitHub API.</summary>
        private HttpResponseMessage Default(Uri uri)
        {
            if (string.Equals(uri.Host, "api.github.com", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"sha\":\"{Commit}\"}}") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(AlphaZip()) };
        }

        public void Dispose()
        {
            this.Skills.Dispose();
            this.dir.Dispose();
        }
    }

    /// <summary>A catalog preview shows the repository, path, commit, publisher, license, name and description.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_KnownEntry_ShowsProvenance()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.NotNull(preview.CatalogEntry);
        Assert.Equal(Repo, preview.RepoUrl);
        Assert.Equal("skills/alpha", preview.Path);
        Assert.Equal(Commit, preview.Commit);
        Assert.Equal("Acme", preview.Publisher);
        Assert.Equal("MIT", preview.License);
        Assert.Equal("alpha", preview.Name);
        Assert.Equal("Alpha.", preview.Description);
        Assert.Empty(preview.FrontmatterDifferences);
        Assert.Empty(preview.ValidationProblems);
        Assert.True(preview.CanInstall);
    }

    /// <summary>An id the catalog does not know has no preview.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_UnknownId_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/nope", ct);

        Assert.Null(preview);
    }

    /// <summary>A description that differs from the catalog is reported in words and does not block the install (SK-5).</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_DescriptionDiffers_ReportsDifference()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(Serve(AlphaZip("---\nname: alpha\ndescription: Changed.\n---\nBody\n")));

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["description: the catalog says 'Alpha.' but the file says 'Changed.'"], preview.FrontmatterDifferences);
        Assert.True(preview.CanInstall);
    }

    /// <summary>A name that differs from the catalog is reported and, because it no longer matches its folder, blocks the install.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_NameDiffers_ReportsDifferenceAndBlocks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(Serve(AlphaZip("---\nname: beta\ndescription: Alpha.\n---\nBody\n")));

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["name: the catalog says 'alpha' but the file says 'beta'"], preview.FrontmatterDifferences);
        Assert.Equal(["SKILL.md's name 'beta' does not match its folder name 'alpha'."], preview.ValidationProblems);
        Assert.False(preview.CanInstall);
    }

    /// <summary>A script file raises a review flag, but flags never block the install.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_ScriptFile_FlagsButStillInstallable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(Serve(AlphaZip(SkillText, ("run.sh", "echo hi\n"))));

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal([new ReviewFlag(ReviewFlagKind.Script, "run.sh", 1, "run.sh")], preview.Flags);
        Assert.True(preview.CanInstall);
    }

    /// <summary>A SKILL.md without a name is a validation problem and blocks the install.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_InvalidFrontmatter_BlocksInstall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(Serve(AlphaZip("---\ndescription: Alpha.\n---\nBody\n")));

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["SKILL.md is missing required field 'name'."], preview.ValidationProblems);
        Assert.False(preview.CanInstall);
    }

    /// <summary>A failed download becomes a preview that cannot be installed, carrying the downloader's message.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_DownloadFails_CannotInstall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(static _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["The download failed with HTTP status 404."], preview.ValidationProblems);
        Assert.False(preview.CanInstall);
    }

    /// <summary>A preview alone writes nothing to disk.</summary>
    [Fact]
    public async Task PreviewFromCatalogAsync_WritesNothingToDisk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        _ = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);

        Assert.Empty(Directory.GetFileSystemEntries(harness.SkillsDir));
        Assert.Empty(harness.LeftoverTempFolders());
    }

    /// <summary>A tree URL pinned to a commit needs no extra network call and takes publisher and license from the catalog.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_CommitUrl_UsesCatalogEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync($"{Repo}/tree/{Commit}/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.NotNull(preview.CatalogEntry);
        Assert.Equal("Acme", preview.Publisher);
        Assert.Equal("MIT", preview.License);
        Assert.Equal(Commit, preview.Commit);
        Assert.Equal(["https://codeload.github.com/acme/skills/zip/" + Commit], harness.Handler.Requests.Select(static r => r.Uri?.AbsoluteUri));
    }

    /// <summary>A branch name is resolved to a commit through the GitHub API so the pin is always a commit.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_BranchUrl_ResolvesCommit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync($"{Repo}/tree/main/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(Commit, preview.Commit);
        Assert.Equal("https://api.github.com/repos/acme/skills/commits/main", harness.Handler.Requests[0].Uri?.AbsoluteUri);
    }

    /// <summary>A repository the catalog does not list gets the owner as publisher, no license and a no-license flag.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_UnknownRepo_UsesOwnerAndNoLicense()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip("other-" + Commit, [("skills/alpha/SKILL.md", SkillText)]);
        using Harness harness = new(Serve(zip));

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync($"https://github.com/someone/other/tree/{Commit}/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Null(preview.CatalogEntry);
        Assert.Equal("someone", preview.Publisher);
        Assert.Equal("none", preview.License);
        Assert.Equal([new ReviewFlag(ReviewFlagKind.NoLicense, ".", 1, "No license declared")], preview.Flags);
    }

    /// <summary>A URL that is not a GitHub tree URL has no preview and makes no request.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_NotATreeUrl_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync("https://example.com/acme/skills/tree/main/skills/alpha", ct);

        Assert.Null(preview);
        Assert.Empty(harness.Handler.Requests);
    }

    /// <summary>A branch the API cannot resolve becomes a preview that cannot be installed.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_BranchNotResolved_CannotInstall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(static _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync($"{Repo}/tree/gone/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["The branch or commit could not be resolved."], preview.ValidationProblems);
        Assert.False(preview.CanInstall);
    }

    /// <summary>An install writes the files and a <c>.source.json</c> carrying the folder hash and the import time.</summary>
    [Fact]
    public async Task Install_WritesFilesAndProvenance()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);
        Assert.NotNull(preview);

        ImportOutcome outcome = harness.Importer.Install(preview);

        string folder = Path.Combine(harness.SkillsDir, "alpha");
        Assert.True(outcome.Installed);
        Assert.Null(outcome.Error);
        Assert.Equal(folder, outcome.SkillFolder);
        Assert.Equal(SkillText, File.ReadAllText(Path.Combine(folder, "SKILL.md")));
        Assert.True(SkillProvenance.TryRead(folder, out SkillProvenance? source));
        Assert.Equal(new SkillProvenance(Repo, "skills/alpha", Commit, SkillFolderHash.Compute(preview.Content.Files), "Acme", "MIT", harness.Clock.GetUtcNow()), source);
        Assert.Equal(SkillFolderHash.Compute(preview.Content.Files), SkillFolderHash.ComputeFolder(folder));
        Assert.Empty(harness.LeftoverTempFolders());
    }

    /// <summary>Installing the same preview twice succeeds and leaves the first record untouched.</summary>
    [Fact]
    public async Task Install_Twice_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);
        Assert.NotNull(preview);
        _ = harness.Importer.Install(preview);
        string recordPath = Path.Combine(harness.SkillsDir, "alpha", SkillProvenance.FileName);
        string before = File.ReadAllText(recordPath);
        harness.Clock.Advance(TimeSpan.FromHours(1));

        ImportOutcome second = harness.Importer.Install(preview);

        Assert.True(second.Installed);
        Assert.Null(second.Error);
        Assert.Equal(before, File.ReadAllText(recordPath));
    }

    /// <summary>A local skill of the same name that was not imported from this source is never overwritten.</summary>
    [Fact]
    public async Task Install_ExistingLocalSkill_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();
        string folder = Path.Combine(harness.SkillsDir, "alpha");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "SKILL.md"), "mine", ct);
        ImportPreview? preview = await harness.Importer.PreviewFromCatalogAsync("acme/skills/alpha", ct);
        Assert.NotNull(preview);

        ImportOutcome outcome = harness.Importer.Install(preview);

        Assert.False(outcome.Installed);
        Assert.Equal("A skill named 'alpha' already exists and was not imported from this source.", outcome.Error);
        Assert.Null(outcome.SkillFolder);
        Assert.Equal("mine", await File.ReadAllTextAsync(Path.Combine(folder, "SKILL.md"), ct));
    }

    /// <summary>A preview that cannot be installed is refused without touching the disk.</summary>
    [Fact]
    public void Install_CannotInstall_IsRefused()
    {
        using Harness harness = new();
        ImportPreview preview = PreviewOf(new Dictionary<string, byte[]>(StringComparer.Ordinal), canInstall: false);

        ImportOutcome outcome = harness.Importer.Install(preview);

        Assert.False(outcome.Installed);
        Assert.Equal("This skill cannot be installed.", outcome.Error);
        Assert.Empty(Directory.GetFileSystemEntries(harness.SkillsDir));
    }

    /// <summary>A write that fails half way leaves neither the skill folder nor the temporary folder behind.</summary>
    [Fact]
    public void Install_WriteFails_LeavesNothingBehind()
    {
        using Harness harness = new();
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes(SkillText),
            ["a"] = [1],
            ["a/b.md"] = [2],
        };

        ImportOutcome outcome = harness.Importer.Install(PreviewOf(files));

        Assert.False(outcome.Installed);
        Assert.Equal("The skill could not be written.", outcome.Error);
        Assert.Empty(Directory.GetFileSystemEntries(harness.SkillsDir));
        Assert.Empty(harness.LeftoverTempFolders());
    }

    /// <summary>A skill named like a built-in is refused, and the message names the built-in.</summary>
    [Fact]
    public void Install_NameOfBuiltInSkill_IsRefused()
    {
        using Harness harness = new();
        ImportPreview preview = PreviewOf(new Dictionary<string, byte[]> { ["SKILL.md"] = Encoding.UTF8.GetBytes(SkillText) }) with { Name = "team-building" };

        ImportOutcome outcome = harness.Importer.Install(preview);

        Assert.False(outcome.Installed);
        Assert.Equal("'team-building' is a built-in skill and cannot be replaced by an import.", outcome.Error);
        Assert.Empty(harness.LeftoverTempFolders());
    }

    /// <summary>An owner, repository or branch segment with a percent, query, fragment or slash character is not a tree URL and sends no request.</summary>
    /// <param name="url">The URL to preview.</param>
    [Theory]
    [InlineData("https://github.com/ac%3Fme/skills/tree/main/x")]
    [InlineData("https://github.com/acme/sk%23ills/tree/main/x")]
    [InlineData("https://github.com/acme/skills/tree/ma%2Fin/x")]
    [InlineData("https://github.com/ac%25me/skills/tree/main/x")]
    public async Task PreviewFromUrlAsync_UnsafeSegment_ReturnsNullWithoutRequest(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new();

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync(url, ct);

        Assert.Null(preview);
        Assert.Empty(harness.Handler.Requests);
    }

    /// <summary>A 403 or 429 from the GitHub API is reported as a rate limit, not as an unresolved branch.</summary>
    /// <param name="status">The HTTP status.</param>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task PreviewFromUrlAsync_ApiRateLimited_SaysSo(HttpStatusCode status)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Harness harness = new(_ => new HttpResponseMessage(status));

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync("https://github.com/acme/skills/tree/main/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.False(preview.CanInstall);
        Assert.Equal(["The GitHub rate limit was reached; try again later."], preview.ValidationProblems);
    }

    /// <summary>An API response over 1 MB is not read, so the branch stays unresolved.</summary>
    [Fact]
    public async Task PreviewFromUrlAsync_OversizedApiResponse_IsUnresolved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string body = "{\"sha\":\"" + Commit + "\",\"pad\":\"" + new string('x', 1024 * 1024 + 10) + "\"}";
        using Harness harness = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

        ImportPreview? preview = await harness.Importer.PreviewFromUrlAsync("https://github.com/acme/skills/tree/main/skills/alpha", ct);

        Assert.NotNull(preview);
        Assert.Equal(["The branch or commit could not be resolved."], preview.ValidationProblems);
    }

    /// <summary>An install removes temporary import folders older than an hour and keeps newer ones.</summary>
    [Fact]
    public void Install_StaleTempFolders_AreRemoved()
    {
        using Harness harness = new();
        string parent = Path.GetDirectoryName(harness.SkillsDir) ?? harness.SkillsDir;
        string stale = Path.Combine(parent, ".skill-import-stale");
        string fresh = Path.Combine(parent, ".skill-import-fresh");
        Directory.CreateDirectory(stale);
        Directory.CreateDirectory(fresh);
        Directory.SetLastWriteTimeUtc(stale, harness.Clock.GetUtcNow().UtcDateTime.AddHours(-2));
        Directory.SetLastWriteTimeUtc(fresh, harness.Clock.GetUtcNow().UtcDateTime.AddMinutes(-5));

        ImportOutcome outcome = harness.Importer.Install(PreviewOf(new Dictionary<string, byte[]> { ["SKILL.md"] = Encoding.UTF8.GetBytes(SkillText) }));

        Assert.True(outcome.Installed);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(fresh));
    }
}