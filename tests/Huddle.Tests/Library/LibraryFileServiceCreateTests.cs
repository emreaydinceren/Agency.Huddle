using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.CreateFileAsync"/> and <see cref="LibraryFileService.CreateFolderAsync"/>
/// (Spec §6.4 create row, §6.11 extensions), and corrections-B4 items 19-23.
/// </summary>
public sealed class LibraryFileServiceCreateTests
{
    /// <summary>A name with no extension gets <c>.md</c> appended, and the new file is empty UTF-8 without BOM.</summary>
    [Fact]
    public async Task CreateFile_NoExtension_AddsMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "notes", ct);

        Assert.NotNull(result.Value);
        string createdPath = Path.Combine(vault, "notes.md");
        Assert.True(File.Exists(createdPath));
        Assert.Equal([], File.ReadAllBytes(createdPath));
        Assert.Equal("notes.md", Path.GetFileName(result.Value.FullPath));
    }

    /// <summary>A name that already carries a known §6.11 extension keeps it as-is.</summary>
    [Fact]
    public async Task CreateFile_WithExtension_Keeps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "data.json", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "data.json")));
        Assert.Equal("data.json", Path.GetFileName(result.Value.FullPath));
    }

    /// <summary>A name with a dot but no known §6.11 extension still gets <c>.md</c> appended (item 23):
    /// "Meeting 3.5" is not a known extension, so it becomes "Meeting 3.5.md".</summary>
    [Fact]
    public async Task CreateFile_DecimalLikeName_AppendsMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "Meeting 3.5", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "Meeting 3.5.md")));
        Assert.Equal("Meeting 3.5.md", Path.GetFileName(result.Value.FullPath));
    }

    /// <summary>A known text extension (item 23) is kept as-is: "notes.txt" stays "notes.txt".</summary>
    [Fact]
    public async Task CreateFile_KnownTextExtension_Keeps()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "notes.txt", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "notes.txt")));
        Assert.Equal("notes.txt", Path.GetFileName(result.Value.FullPath));
    }

    /// <summary>A name with no known §6.11 extension but capitalised gets <c>.md</c> appended (item 23):
    /// "Plan" becomes "Plan.md".</summary>
    [Fact]
    public async Task CreateFile_NoExtensionCapitalised_AppendsMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "Plan", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "Plan.md")));
        Assert.Equal("Plan.md", Path.GetFileName(result.Value.FullPath));
    }

    /// <summary>An invalid name is refused with the Spec §6.1 per-segment text, and nothing is created.</summary>
    [Fact]
    public async Task CreateFile_InvalidName_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "a*b", ct);

        Assert.Null(result.Value);
        Assert.Equal("A name can't contain *.", result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(vault));
    }

    /// <summary>Creating a file whose name already exists as a FILE is refused with the settled exists text,
    /// and the existing file is untouched (corrections-B4 item 19).</summary>
    [Fact]
    public async Task CreateFile_Exists_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string existingPath = Path.Combine(vault, "notes.md");
        File.WriteAllText(existingPath, "original");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "notes.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("\"notes.md\" already exists here.", result.Error);
        Assert.Equal("original", File.ReadAllText(existingPath));
    }

    /// <summary>Creating a file whose name already exists as a FOLDER is refused with the settled exists text,
    /// and nothing is overwritten (corrections-B4 item 19).</summary>
    [Fact]
    public async Task CreateFile_ExistsAsFolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string existingFolder = Path.Combine(vault, "notes.md");
        Directory.CreateDirectory(existingFolder);
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(folder, "notes.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("\"notes.md\" already exists here.", result.Error);
        Assert.True(Directory.Exists(existingFolder));
    }

    /// <summary>Creates a folder that doesn't yet exist.</summary>
    [Fact]
    public async Task CreateFolder_Creates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(folder, "Drafts", ct);

        Assert.NotNull(result.Value);
        Assert.True(Directory.Exists(Path.Combine(vault, "Drafts")));
    }

    /// <summary>Creating a folder over an EXISTING folder is refused with the settled exists text, not a silent
    /// success (corrections-B4 item 20: existence is checked before the idempotent <see cref="Directory.CreateDirectory(string)"/>).</summary>
    [Fact]
    public async Task CreateFolder_Exists_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string existingFolder = Path.Combine(vault, "Drafts");
        Directory.CreateDirectory(existingFolder);
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(folder, "Drafts", ct);

        Assert.Null(result.Value);
        Assert.Equal("\"Drafts\" already exists here.", result.Error);
    }

    /// <summary>A reserved <c>_</c>-prefixed folder name under a Team is refused with the settled reserved text.</summary>
    [Fact]
    public async Task CreateFolder_UnderTeamsUnderscore_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        Directory.CreateDirectory(marketing);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamFolder = fixture.ResolveTeams("Marketing");

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(teamFolder, "_x", ct);

        Assert.Null(result.Value);
        Assert.Equal("That folder is reserved.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(marketing, "_x")));
    }

    /// <summary>Creating a folder directly in the Teams root creates a Team folder; the returned path's Role is
    /// the re-resolved one (corrections-B4 item 22).</summary>
    [Fact]
    public async Task CreateFolder_InTeamsRoot_CreatesTeamFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsRootPath = fixture.ResolveTeams(string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(teamsRootPath, "Sales", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.TeamFolder, result.Value.Role);
        Assert.True(Directory.Exists(Path.Combine(fixture.DataDir, "Teams", "Sales")));
    }

    /// <summary>Creating a folder inside a Team folder produces a Project folder: the returned path's Role is
    /// the re-resolved one, not a caller guess (corrections-B4 item 22).</summary>
    [Fact]
    public async Task CreateFolder_UnderTeamFolder_ReturnsProjectFolderRole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        Directory.CreateDirectory(marketing);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamFolder = fixture.ResolveTeams("Marketing");

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(teamFolder, "Launch Q4", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.ProjectFolder, result.Value.Role);
        Assert.True(Directory.Exists(Path.Combine(marketing, "Launch Q4")));
    }

    /// <summary>A folder named <c>memory</c> under a Team is a plain Folder, not a Project: the returned Role is
    /// <see cref="LibraryNodeRole.Folder"/>, so Library protection allows rename, move and delete on it, and a
    /// file can be created in it (corrections-D6 6.0.t).</summary>
    [Fact]
    public async Task CreateFolder_MemoryUnderTeam_IsPlainFolderThatAcceptsFiles()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string marketing = Path.Combine(fixture.DataDir, "Teams", "Marketing");
        Directory.CreateDirectory(marketing);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamFolder = fixture.ResolveTeams("Marketing");

        LibraryResult<LibraryPath> folderResult = await service.CreateFolderAsync(teamFolder, "memory", ct);

        Assert.NotNull(folderResult.Value);
        Assert.Equal(LibraryNodeRole.Folder, folderResult.Value.Role);
        Assert.True(Directory.Exists(Path.Combine(marketing, "memory")));
        LibraryProtection protection = LibraryProtection.For(folderResult.Value);
        Assert.True(protection.CanRename);
        Assert.True(protection.CanMove);
        Assert.True(protection.CanDelete);
        Assert.Null(protection.Reason);

        LibraryResult<LibraryPath> fileResult = await service.CreateFileAsync(folderResult.Value, "fact", ct);

        Assert.NotNull(fileResult.Value);
        Assert.True(File.Exists(Path.Combine(marketing, "memory", "fact.md")));
    }

    /// <summary>Creating anything directly at the Teammates root is refused (corrections-B4 item 21, settled text).</summary>
    [Fact]
    public async Task CreateFolder_AtTeammatesRoot_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teammatesRoot = fixture.ResolveTeammatesFolder(string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFolderAsync(teammatesRoot, "someone", ct);

        Assert.Null(result.Value);
        Assert.Equal("Add teammates on the Teammates page.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.DataDir, "Teammates", "someone")));
    }

    /// <summary>Creating a file directly at the Teammates root is refused (corrections-B4 item 21, settled text).</summary>
    [Fact]
    public async Task CreateFile_AtTeammatesRoot_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teammatesRoot = fixture.ResolveTeammatesFolder(string.Empty);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(teammatesRoot, "notes", ct);

        Assert.Null(result.Value);
        Assert.Equal("Add teammates on the Teammates page.", result.Error);
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, "Teammates", "notes.md")));
    }

    /// <summary>Creating a <c>.md</c> file directly in a Teammate folder is refused (corrections-B4 item 21,
    /// settled text): a Teammate folder holds only its definition.</summary>
    [Fact]
    public async Task CreateFile_MdDirectlyInTeammateFolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        LibraryFileService service = fixture.CreateService();
        LibraryPath teammateFolder = fixture.ResolveTeammatesFolder("ada");

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(teammateFolder, "notes", ct);

        Assert.Null(result.Value);
        Assert.Equal("A teammate folder holds only its definition; use work/.", result.Error);
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, "Teammates", "ada", "notes.md")));
    }

    /// <summary>Creating a file inside a Teammate's <c>work/</c> folder succeeds (corrections-B4 item 21: the
    /// refusal is only for the Teammate folder itself, not its Work Dir).</summary>
    [Fact]
    public async Task CreateFile_UnderTeammateWorkFolder_Succeeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        string workDir = Path.Combine(fixture.DataDir, "Teammates", "ada", "work");
        Directory.CreateDirectory(workDir);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath workFolder = fixture.ResolveTeammatesFolder("ada/work");

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(workFolder, "x", ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(workDir, "x.md")));
    }
}
