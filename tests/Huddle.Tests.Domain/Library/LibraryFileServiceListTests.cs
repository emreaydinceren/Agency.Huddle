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
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "A"));
        Directory.CreateDirectory(Path.Combine(vault, "C"));
        File.WriteAllText(Path.Combine(vault, "b.md"), string.Empty);
        File.WriteAllText(Path.Combine(vault, "a.md"), string.Empty);
        LibraryFileService service = fixture.CreateService();
        LibraryPath root = fixture.Resolve(vault, string.Empty);

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(root, ct);

        Assert.Equal(["A", "C", "a.md", "b.md"], [.. entries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>The <c>.obsidian</c>, <c>.trash</c> and <c>.git</c> folders never appear, regardless of configuration.</summary>
    [Fact]
    public async Task ListAsync_HidesObsidianTrashGit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, ".obsidian"));
        Directory.CreateDirectory(Path.Combine(vault, ".trash"));
        Directory.CreateDirectory(Path.Combine(vault, ".git"));
        LibraryFileService service = fixture.CreateService();
        LibraryPath root = fixture.Resolve(vault, string.Empty);

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(root, ct);

        Assert.Empty(entries);
    }

    /// <summary>Folders named in the effective File Changes ignore list are hidden; a custom list replaces the default rather than adding to it.</summary>
    [Fact]
    public async Task ListAsync_HidesFileChangesIgnore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture defaultFixture = LibraryFileServiceFixture.Build();
        string defaultVault = defaultFixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(defaultVault, "node_modules"));
        LibraryFileService defaultService = defaultFixture.CreateService();
        LibraryPath defaultRoot = defaultFixture.Resolve(defaultVault, string.Empty);

        IReadOnlyList<LibraryEntry> defaultEntries = await defaultService.ListAsync(defaultRoot, ct);

        Assert.Empty(defaultEntries);

        using LibraryFileServiceFixture customFixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.FileChanges.Ignore = ["out"]);
        string customVault = customFixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(customVault, "out"));
        Directory.CreateDirectory(Path.Combine(customVault, "node_modules"));
        LibraryFileService customService = customFixture.CreateService();
        LibraryPath customRoot = customFixture.Resolve(customVault, string.Empty);

        IReadOnlyList<LibraryEntry> customEntries = await customService.ListAsync(customRoot, ct);

        Assert.Equal(["node_modules"], [.. customEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>Under the Teams root, an underscore-prefixed folder is hidden; the same shape under a Teammate's work folder is not, since the rule is Teams-only.</summary>
    [Fact]
    public async Task ListAsync_UnderTeams_HidesUnderscoreFolders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing", "_tasks"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teams", "Marketing", "notes.md"), string.Empty);
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teammates", "Nova", "work", "_x"));
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teammates", "Nova", "work", "memory"));
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsFolder = fixture.ResolveTeams("Marketing");
        LibraryPath teammateWork = fixture.ResolveTeammatesFolder("Nova/work");

        IReadOnlyList<LibraryEntry> teamsEntries = await service.ListAsync(teamsFolder, ct);
        IReadOnlyList<LibraryEntry> teammateEntries = await service.ListAsync(teammateWork, ct);

        Assert.Equal(["notes.md"], [.. teamsEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
        Assert.Equal(["memory", "_x"], [.. teammateEntries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>The app's own <c>.layout-migrated</c> marker in the Teammates root is internal state and is not listed, so nobody can try to rename or delete it.</summary>
    [Fact]
    public async Task ListAsync_TeammatesRoot_HidesTheLayoutMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teammates", "Nova"));
        File.WriteAllText(Path.Combine(fixture.DataDir, "Teammates", ".layout-migrated"), string.Empty);
        LibraryFileService service = fixture.CreateService();

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(fixture.ResolveTeammatesFolder(string.Empty), ct);

        Assert.Equal(["Nova"], [.. entries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }

    /// <summary>An entry carries the resolved role, the file's length and its last-write time.</summary>
    [Fact]
    public async Task ListAsync_Entries_CarryRoleLengthAndTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));
        string filePath = Path.Combine(fixture.DataDir, "Teams", "readme.md");
        File.WriteAllText(filePath, "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsRoot = fixture.ResolveTeams(string.Empty);

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
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath ghost = fixture.ResolveTeams("Ghost");

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(ghost, ct);

        Assert.Empty(entries);
    }

    /// <summary>Listing a path that is an existing FILE does not throw (it used to surface an IOException as an HTTP 500) and yields nothing.</summary>
    [Fact]
    public async Task ListAsync_FilePath_ReturnsEmptyWithoutThrowing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "x");
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "note.md");

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(file, ct);

        Assert.Empty(entries);
    }

    /// <summary>Listing a normal folder still returns its children after the file guard.</summary>
    [Fact]
    public async Task ListAsync_NormalFolder_StillReturnsChildren()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Sub"));
        File.WriteAllText(Path.Combine(vault, "note.md"), "x");
        LibraryFileService service = fixture.CreateService();

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(fixture.Resolve(vault, string.Empty), ct);

        Assert.Equal(["Sub", "note.md"], [.. entries.Select(e => Path.GetFileName(e.Path.FullPath))]);
    }
}
