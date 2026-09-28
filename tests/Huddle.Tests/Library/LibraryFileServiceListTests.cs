using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.ListAsync"/> (Spec §6.4): sort order, the folders hidden
/// everywhere, <see cref="FileChangesOptions.EffectiveIgnore"/>, the Teams-only underscore rule, the
/// role/length/time an entry carries, and a not-yet-created Team folder (Spec §6.16).
/// </summary>
public sealed class LibraryFileServiceListTests
{
    /// <summary>Children sort folders first, then <see cref="StringComparer.OrdinalIgnoreCase"/> by name.</summary>
    [Fact]
    public async Task ListAsync_FoldersFirst_ThenOrdinalIgnoreCase()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = Fixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "A"));
        Directory.CreateDirectory(Path.Combine(vault, "C"));
        File.WriteAllText(Path.Combine(vault, "b.md"), string.Empty);
        File.WriteAllText(Path.Combine(vault, "a.md"), string.Empty);
        LibraryFileService service = fixture.CreateService();
        LibraryPath root = fixture.ResolveRoot("Vault");

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(root, ct);

        Assert.Equal(["A", "C", "a.md", "b.md"], [.. entries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>The <c>.obsidian</c>, <c>.trash</c> and <c>.git</c> folders never appear, regardless of configuration.</summary>
    [Fact]
    public async Task ListAsync_HidesObsidianTrashGit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = Fixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, ".obsidian"));
        Directory.CreateDirectory(Path.Combine(vault, ".trash"));
        Directory.CreateDirectory(Path.Combine(vault, ".git"));
        LibraryFileService service = fixture.CreateService();
        LibraryPath root = fixture.ResolveRoot("Vault");

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(root, ct);

        Assert.Empty(entries);
    }

    /// <summary>Folders named in the effective File Changes ignore list are hidden; a custom list replaces the default rather than adding to it.</summary>
    [Fact]
    public async Task ListAsync_HidesFileChangesIgnore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture defaultFixture = Fixture.Build();
        string defaultVault = defaultFixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(defaultVault, "node_modules"));
        LibraryFileService defaultService = defaultFixture.CreateService();
        LibraryPath defaultRoot = defaultFixture.ResolveRoot("Vault");

        IReadOnlyList<LibraryEntry> defaultEntries = await defaultService.ListAsync(defaultRoot, ct);

        Assert.Empty(defaultEntries);

        using Fixture customFixture = Fixture.Build(teamOptions => teamOptions.FileChanges.Ignore = ["out"]);
        string customVault = customFixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(customVault, "out"));
        Directory.CreateDirectory(Path.Combine(customVault, "node_modules"));
        LibraryFileService customService = customFixture.CreateService();
        LibraryPath customRoot = customFixture.ResolveRoot("Vault");

        IReadOnlyList<LibraryEntry> customEntries = await customService.ListAsync(customRoot, ct);

        Assert.Equal(["node_modules"], [.. customEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>Under the Teams root, an underscore-prefixed folder is hidden; the same shape under a Teammate's work folder is not, since the rule is Teams-only.</summary>
    [Fact]
    public async Task ListAsync_UnderTeams_HidesUnderscoreFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "_tasks"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "notes.md"), string.Empty);
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teammates", "Nova", "work", "_x"));
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teammates", "Nova", "work", "memory"));
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsFolder = fixture.Resolve("teams", "Marketing");
        LibraryPath teammateWork = fixture.Resolve("teammates", "Nova/work");

        IReadOnlyList<LibraryEntry> teamsEntries = await service.ListAsync(teamsFolder, ct);
        IReadOnlyList<LibraryEntry> teammateEntries = await service.ListAsync(teammateWork, ct);

        Assert.Equal(["notes.md"], [.. teamsEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
        Assert.Equal(["memory", "_x"], [.. teammateEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>An entry carries the resolved role, the file's length and its last-write time.</summary>
    [Fact]
    public async Task ListAsync_Entries_CarryRoleLengthAndTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = Fixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        string filePath = Path.Combine(fixture.DataDir, "Teams", "readme.md");
        File.WriteAllText(filePath, "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsRoot = fixture.Resolve("teams", string.Empty);

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(teamsRoot, ct);

        LibraryEntry marketing = Assert.Single(entries, e => string.Equals(Path.GetFileName(e.Path.FullPath), "Marketing", StringComparison.Ordinal));
        Assert.Equal(LibraryNodeRole.TeamFolder, marketing.Path.Role);

        LibraryEntry readme = Assert.Single(entries, e => string.Equals(Path.GetFileName(e.Path.FullPath), "readme.md", StringComparison.Ordinal));
        FileInfo info = new(filePath);
        Assert.Equal(info.Length, readme.Length);
        Assert.Equal(new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), readme.LastWriteUtc);
    }

    /// <summary>A Team folder not yet created on disk lists as empty rather than throwing (Spec §6.16).</summary>
    [Fact]
    public async Task ListAsync_MissingFolder_ReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = Fixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath ghost = fixture.Resolve("teams", "Ghost");

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(ghost, ct);

        Assert.Empty(entries);
    }

    /// <summary>A minimal <see cref="IRecycleBin"/> fake: this task never calls it, only satisfies the constructor (6.8.i implements the real behaviour).</summary>
    private sealed class RecordingRecycleBin : IRecycleBin
    {
        /// <summary>Every path passed to <see cref="TrySend"/> so far, in call order.</summary>
        public List<string> Sent { get; } = [];

        /// <summary>Records <paramref name="fullPath"/> and always succeeds.</summary>
        /// <param name="fullPath">The path this fake records.</param>
        /// <param name="error">Always <see langword="null"/>.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
        {
            this.Sent.Add(fullPath);
            error = null;
            return true;
        }
    }

    /// <summary>An isolated fixture: a real <see cref="LibraryRootStore"/> and <see cref="LibraryPathResolver"/> over a temp <c>DataDir</c>, plus the <see cref="RecordingRecycleBin"/> fake.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir temp;
        private readonly TeamOptions teamOptions;

        private Fixture(TempDataDir temp, TeamOptions teamOptions)
        {
            this.temp = temp;
            this.teamOptions = teamOptions;
            this.DataDir = teamOptions.DataDir;
            TeammatePaths paths = new(Options.Create(teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public string DataDir { get; }

        public LibraryRootStore RootStore { get; private set; }

        public LibraryPathResolver Resolver { get; private set; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout, optionally customising <see cref="TeamOptions"/> first.</summary>
        public static Fixture Build(Action<TeamOptions>? configure = null)
        {
            TempDataDir temp = new();
            Directory.CreateDirectory(Path.Combine(temp.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(temp.Path, "Teammates"));

            TeamOptions teamOptions = new() { DataDir = temp.Path };
            configure?.Invoke(teamOptions);

            return new Fixture(temp, teamOptions);
        }

        /// <summary>Creates a pinned root folder named <paramref name="name"/> under the temp root and reloads the store and resolver.</summary>
        public string CreatePinnedRoot(string name)
        {
            string path = Path.Combine(this.temp.Path, name);
            Directory.CreateDirectory(path);

            List<PinnedRootOption> roots = [.. this.teamOptions.Library.Roots ?? [], new PinnedRootOption { Name = name, Path = path }];
            this.teamOptions.Library.Roots = roots;
            this.Reload();
            return path;
        }

        /// <summary>Constructs the service under test, wired to this fixture's resolver and a fresh <see cref="RecordingRecycleBin"/>.</summary>
        public LibraryFileService CreateService() => new(
            this.Resolver,
            new RecordingRecycleBin(),
            new WikiLinkIndex(this.RootStore, this.Resolver, Options.Create(this.teamOptions)),
            Options.Create(this.teamOptions),
            NullLogger<LibraryFileService>.Instance);

        /// <summary>Resolves a pinned root by name.</summary>
        public LibraryPath ResolveRoot(string pinnedRootName)
        {
            foreach (LibraryRoot candidate in this.RootStore.Roots)
            {
                if (string.Equals(candidate.DisplayName, pinnedRootName, StringComparison.Ordinal))
                {
                    Assert.True(this.Resolver.TryResolve(candidate.Id, string.Empty, out LibraryPath? resolved, out _));
                    Assert.NotNull(resolved);
                    return resolved;
                }
            }

            throw new InvalidOperationException($"No pinned root named '{pinnedRootName}'.");
        }

        /// <summary>Resolves <paramref name="relativePath"/> against <paramref name="rootId"/>, asserting it succeeds.</summary>
        public LibraryPath Resolve(string rootId, string relativePath)
        {
            Assert.True(this.Resolver.TryResolve(rootId, relativePath, out LibraryPath? path, out _));
            Assert.NotNull(path);
            return path;
        }

        private void Reload()
        {
            TeammatePaths paths = new(Options.Create(this.teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(this.teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(this.teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public void Dispose() => this.temp.Dispose();
    }
}
