using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.RenameAsync"/> and <see cref="LibraryFileService.MoveAsync"/>
/// (Spec §6.4 rename/move row, minus the link rewrite (D8)), and corrections-B4 items 24-28.
/// </summary>
public sealed class LibraryFileServiceMoveTests
{
    /// <summary>Renaming a file moves it on disk under its new name, and the result's lists are empty
    /// (D8 fills them later).</summary>
    [Fact]
    public async Task Rename_File_Moves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string oldPath = Path.Combine(vault, "old.md");
        File.WriteAllText(oldPath, "hello");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "old.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(file, "new.md", ct);

        Assert.NotNull(result.Value);
        string newPath = Path.Combine(vault, "new.md");
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(newPath));
        Assert.Equal("hello", File.ReadAllText(newPath));
        Assert.Equal(LibraryNodeRole.File, result.Value.NewPath.Role);
        Assert.Empty(result.Value.RewrittenNotes);
        Assert.Empty(result.Value.FailedNotes);
    }

    /// <summary>Renaming a folder moves the whole subtree, preserving its contents.</summary>
    [Fact]
    public async Task Rename_Folder_MovesContents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string oldFolder = Path.Combine(vault, "OldFolder");
        Directory.CreateDirectory(oldFolder);
        File.WriteAllText(Path.Combine(oldFolder, "inside.md"), "body");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, "OldFolder");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(folder, "NewFolder", ct);

        Assert.NotNull(result.Value);
        string newFolder = Path.Combine(vault, "NewFolder");
        Assert.False(Directory.Exists(oldFolder));
        Assert.True(Directory.Exists(newFolder));
        Assert.True(File.Exists(Path.Combine(newFolder, "inside.md")));
        Assert.Equal(LibraryNodeRole.Folder, result.Value.NewPath.Role);
    }

    /// <summary>Renaming a protected item (Team/Project folder, Teammate folder, its definition, its Work
    /// Dir, or a root) is refused with the matching Protected refusals text; nothing moves.</summary>
    [Theory]
    [InlineData("TeamFolder")]
    [InlineData("TeammateFolder")]
    [InlineData("TeammateDefinition")]
    [InlineData("WorkDir")]
    [InlineData("Root")]
    public async Task Rename_Protected_Refused(string kind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        string workDir = Path.Combine(fixture.DataDir, "Teammates", "ada", "work");
        Directory.CreateDirectory(workDir);
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        Directory.CreateDirectory(marketing);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();

        (LibraryPath item, string expectedError, string sourcePath) = kind switch
        {
            "TeamFolder" => (fixture.ResolveTeams("Marketing"), "Team and Project folders can't be renamed, moved or deleted here.", marketing),
            "TeammateFolder" => (fixture.ResolveTeammatesFolder("ada"), "Teammate folders can't be renamed, moved or deleted here.", Path.Combine(fixture.DataDir, "Teammates", "ada")),
            "TeammateDefinition" => (fixture.ResolveTeammates("ada"), "Teammate folders can't be renamed, moved or deleted here.", Path.Combine(fixture.DataDir, "Teammates", "ada", "ada.md")),
            "WorkDir" => (fixture.ResolveTeammatesFolder("ada/work"), "Teammate folders can't be renamed, moved or deleted here.", workDir),
            "Root" => (fixture.ResolveTeams(string.Empty), "A Library root can't be renamed, moved or deleted.", teamsRoot),
            _ => throw new InvalidOperationException($"Unknown kind: {kind}"),
        };

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(item, "renamed", ct);

        Assert.Null(result.Value);
        Assert.Equal(expectedError, result.Error);
        Assert.True(Directory.Exists(sourcePath) || File.Exists(sourcePath));
        string? parent = Path.GetDirectoryName(sourcePath);
        Assert.NotNull(parent);
        Assert.False(Directory.Exists(Path.Combine(parent, "renamed")));
    }

    /// <summary>Renaming to a name that already exists in the same folder is refused; nothing moves.</summary>
    [Fact]
    public async Task Rename_TargetExists_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string sourcePath = Path.Combine(vault, "source.md");
        string targetPath = Path.Combine(vault, "target.md");
        File.WriteAllText(sourcePath, "a");
        File.WriteAllText(targetPath, "b");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "source.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(file, "target.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("\"target.md\" already exists here.", result.Error);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal("b", File.ReadAllText(targetPath));
    }

    /// <summary>Renaming to an invalid name (an invalid character) is refused; nothing moves.</summary>
    [Fact]
    public async Task Rename_InvalidName_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string sourcePath = Path.Combine(vault, "source.md");
        File.WriteAllText(sourcePath, "a");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "source.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(file, "bad:name.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("A name can't contain :.", result.Error);
        Assert.True(File.Exists(sourcePath));
    }

    /// <summary>Moving an item to a different Library root is refused with the settled text (corrections-B4
    /// item 26/settled-here); nothing moves.</summary>
    [Fact]
    public async Task Move_AcrossRoots_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string sourcePath = Path.Combine(vault, "note.md");
        File.WriteAllText(sourcePath, "a");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "note.md");
        LibraryPath teamsRoot = fixture.ResolveTeams(string.Empty);

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(file, teamsRoot, ct);

        Assert.Null(result.Value);
        Assert.Equal("Items can't be moved between Library roots.", result.Error);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, "Teams", "note.md")));
    }

    /// <summary>Moving a folder into its own subfolder is refused (settled here); a sibling folder sharing the
    /// same name prefix is a distinct folder and is allowed; a case-different spelling of "into itself" is
    /// still refused when the platform comparer is case-insensitive (corrections-B4 item 25).</summary>
    [Fact]
    public async Task Move_IntoOwnSubfolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folderA = Path.Combine(vault, "A");
        string subfolderB = Path.Combine(folderA, "B");
        Directory.CreateDirectory(subfolderB);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath itemA = fixture.Resolve(vault, "A");
        LibraryPath destination = fixture.Resolve(vault, "A/B");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(itemA, destination, ct);

        Assert.Null(result.Value);
        Assert.Equal("A folder can't be moved into itself.", result.Error);
        Assert.True(Directory.Exists(folderA));
        Assert.False(Directory.Exists(Path.Combine(subfolderB, "A")));
    }

    /// <summary>A sibling folder that shares "A" as a name prefix ("AB") is not "into itself": moving A into AB
    /// is allowed (corrections-B4 item 25).</summary>
    [Fact]
    public async Task Move_IntoSiblingSharingPrefix_Allowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folderA = Path.Combine(vault, "A");
        string folderAb = Path.Combine(vault, "AB");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderAb);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath itemA = fixture.Resolve(vault, "A");
        LibraryPath destination = fixture.Resolve(vault, "AB");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(itemA, destination, ct);

        Assert.NotNull(result.Value);
        Assert.False(Directory.Exists(folderA));
        Assert.True(Directory.Exists(Path.Combine(folderAb, "A")));
    }

    /// <summary>Moving a folder into a differently-cased spelling of its own subfolder is still "into itself"
    /// when the platform's <see cref="FolderSnapshot.PathComparer"/> is case-insensitive (corrections-B4 item 25).</summary>
    [Fact]
    public async Task Move_IntoOwnSubfolder_DifferentCase_RefusedWhenCaseInsensitive()
    {
        if (!FolderSnapshot.PathComparer.Equals("a", "A"))
        {
            Assert.Skip("This platform's PathComparer is case-sensitive: 'a' does not resolve to 'A'.");
            return;
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folderA = Path.Combine(vault, "A");
        string subfolderB = Path.Combine(folderA, "B");
        Directory.CreateDirectory(subfolderB);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath itemA = fixture.Resolve(vault, "a");
        LibraryPath destination = fixture.Resolve(vault, "A/B");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(itemA, destination, ct);

        Assert.Null(result.Value);
        Assert.Equal("A folder can't be moved into itself.", result.Error);
        Assert.True(Directory.Exists(folderA));
    }

    /// <summary>Moving a folder to itself (the destination equals the source) is "into itself" too
    /// (corrections-B4 item 25).</summary>
    [Fact]
    public async Task Move_IntoItself_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folderA = Path.Combine(vault, "A");
        Directory.CreateDirectory(folderA);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath itemA = fixture.Resolve(vault, "A");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(itemA, itemA, ct);

        Assert.Null(result.Value);
        Assert.Equal("A folder can't be moved into itself.", result.Error);
        Assert.True(Directory.Exists(folderA));
    }

    /// <summary>Moving a file into a Project folder succeeds.</summary>
    [Fact]
    public async Task Move_File_ToProjectFolder_Moves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        string launch = Path.Combine(marketing, "Launch");
        Directory.CreateDirectory(launch);
        string sourcePath = Path.Combine(teamsRoot, "brief.md");
        File.WriteAllText(sourcePath, "content");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.ResolveTeams("brief.md");
        LibraryPath projectFolder = fixture.ResolveTeams("Marketing/Launch");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(file, projectFolder, ct);

        Assert.NotNull(result.Value);
        Assert.False(File.Exists(sourcePath));
        string movedPath = Path.Combine(launch, "brief.md");
        Assert.True(File.Exists(movedPath));
        Assert.Equal("content", File.ReadAllText(movedPath));
        Assert.Equal(LibraryNodeRole.File, result.Value.NewPath.Role);
    }

    /// <summary>A case-only rename (a new ordinal spelling that the platform comparer treats as equal) routes
    /// through a temporary sibling and leaves the file with the new casing and no leftover temp sibling
    /// (corrections-B4 item 24).</summary>
    [Fact]
    public async Task Rename_CaseOnly_Renames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string sourcePath = Path.Combine(vault, "note.md");
        File.WriteAllText(sourcePath, "text");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.Resolve(vault, "note.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(file, "Note.md", ct);

        Assert.NotNull(result.Value);
        string[] onDisk = Directory.GetFileSystemEntries(vault);
        Assert.Single(onDisk);
        Assert.Equal("Note.md", Path.GetFileName(onDisk[0]));
        Assert.DoesNotContain(onDisk, entry => Path.GetFileName(entry).Contains(".renaming-", StringComparison.Ordinal));
    }

    /// <summary>Moving a file directly into the Teammates root is refused with item 21's settled text, applied
    /// to move destinations (corrections-B4 item 26).</summary>
    [Fact]
    public async Task Move_File_IntoTeammatesRoot_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teammatesRoot = Path.Combine(fixture.DataDir, "Teammates");
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        string workDir = Path.Combine(teammatesRoot, "ada", "work");
        Directory.CreateDirectory(workDir);
        string sourcePath = Path.Combine(workDir, "note.md");
        File.WriteAllText(sourcePath, "a");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.ResolveTeammatesFolder("ada/work/note.md");
        LibraryPath teammatesRootPath = fixture.ResolveTeammatesFolder(string.Empty);

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(file, teammatesRootPath, ct);

        Assert.Null(result.Value);
        Assert.Equal("Add teammates on the Teammates page.", result.Error);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(teammatesRoot, "note.md")));
    }

    /// <summary>Moving a <c>.md</c> file into an existing Teammate folder is refused with item 21's settled
    /// text (corrections-B4 item 26).</summary>
    [Fact]
    public async Task Move_MdFile_IntoTeammateFolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        string teammatesRoot = Path.Combine(fixture.DataDir, "Teammates");
        string workDir = Path.Combine(teammatesRoot, "ada", "work");
        Directory.CreateDirectory(workDir);
        string sourcePath = Path.Combine(workDir, "note.md");
        File.WriteAllText(sourcePath, "a");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.ResolveTeammatesFolder("ada/work/note.md");
        LibraryPath teammateFolder = fixture.ResolveTeammatesFolder("ada");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(file, teammateFolder, ct);

        Assert.Null(result.Value);
        Assert.Equal("A teammate folder holds only its definition; use work/.", result.Error);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(teammatesRoot, "ada", "note.md")));
    }

    /// <summary>Renaming a non-<c>.md</c> file sitting directly in a Teammate folder to a <c>.md</c> name is
    /// refused with item 21's text, applied to rename destinations too (corrections-B4 item 26).</summary>
    [Fact]
    public async Task Rename_NonMdFileInTeammateFolder_ToMd_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        string teammatesRoot = Path.Combine(fixture.DataDir, "Teammates");
        string sourcePath = Path.Combine(teammatesRoot, "ada", "pic.png");
        File.WriteAllBytes(sourcePath, [1, 2, 3]);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath file = fixture.ResolveTeammatesFolder("ada/pic.png");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(file, "pic.md", ct);

        Assert.Null(result.Value);
        Assert.Equal("A teammate folder holds only its definition; use work/.", result.Error);
        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(teammatesRoot, "ada", "pic.md")));
    }

    /// <summary>Renaming a folder while a file inside it is held open without <see cref="FileShare.Delete"/>
    /// fails with the in-use text (corrections-B4 item 27); nothing moves. Windows-only: the POSIX
    /// implementations this repo targets otherwise allow moving open files.</summary>
    [Fact]
    public async Task Rename_FolderWithFileHeldOpen_Refused()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("File-in-use rename refusal is Windows-only behaviour.");
            return;
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folder = Path.Combine(vault, "Locked");
        Directory.CreateDirectory(folder);
        string heldPath = Path.Combine(folder, "held.md");
        File.WriteAllText(heldPath, "a");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath folderPath = fixture.Resolve(vault, "Locked");

        using FileStream held = new(heldPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(folderPath, "Renamed", ct);

        Assert.Null(result.Value);
        Assert.Equal("Couldn't move Locked: something inside it is in use.", result.Error);
        Assert.True(Directory.Exists(folder));
        Assert.False(Directory.Exists(Path.Combine(vault, "Renamed")));
    }

    /// <summary>Moving a plain folder whose subtree holds a <c>_tasks</c> folder into a Team folder (which
    /// would make it a Project folder, at the wrong depth, turning those files live as Tasks) is refused with
    /// the settled text (corrections-B4 item 28).</summary>
    [Fact]
    public async Task Move_FolderWithTasksSubtree_IntoTeamFolder_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        string oldFolder = Path.Combine(marketing, "Launch", "Old");
        string tasksFolder = Path.Combine(oldFolder, "_tasks");
        Directory.CreateDirectory(tasksFolder);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath old = fixture.ResolveTeams("Marketing/Launch/Old");
        LibraryPath marketingFolder = fixture.ResolveTeams("Marketing");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(old, marketingFolder, ct);

        Assert.Null(result.Value);
        Assert.Equal("Folders that hold tasks can't be moved here.", result.Error);
        Assert.True(Directory.Exists(oldFolder));
        Assert.False(Directory.Exists(Path.Combine(marketing, "Old")));
    }

    /// <summary>The same folder shape without a <c>_tasks</c> subtree moves fine (corrections-B4 item 28: the
    /// refusal is specifically about live Tasks, not depth alone).</summary>
    [Fact]
    public async Task Move_FolderWithoutTasksSubtree_IntoTeamFolder_Moves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        string oldFolder = Path.Combine(marketing, "Launch", "Old");
        Directory.CreateDirectory(oldFolder);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath old = fixture.ResolveTeams("Marketing/Launch/Old");
        LibraryPath marketingFolder = fixture.ResolveTeams("Marketing");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(old, marketingFolder, ct);

        Assert.NotNull(result.Value);
        Assert.False(Directory.Exists(oldFolder));
        Assert.True(Directory.Exists(Path.Combine(marketing, "Old")));
    }
}
