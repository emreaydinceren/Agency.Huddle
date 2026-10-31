using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.RecycleAsync"/> (Spec §6.4 recycle row, §10 E-8).
/// </summary>
public sealed class LibraryFileServiceRecycleTests
{
    /// <summary>Recycling a file re-resolves the caller's path and sends the RESOLVED full path to the
    /// recycle bin, not the one on the stale <see cref="LibraryPath"/> the caller handed in; the service
    /// itself never deletes the file, and the result carries the resolved path.</summary>
    [Fact]
    public async Task Recycle_File_SendsToBin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath resolved = fixture.Resolve(vault, "note.md");
        LibraryPath stale = resolved with { FullPath = Path.Combine(vault, "bogus-stale-path.md") };

        LibraryResult<LibraryPath> result = await service.RecycleAsync(stale, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(filePath, result.Value.FullPath);
        Assert.Equal(filePath, Assert.Single(fixture.RecycleBin.Sent));
        Assert.True(File.Exists(filePath));
    }

    /// <summary>Recycling a folder sends its resolved full path to the bin; the service does not delete it.</summary>
    [Fact]
    public async Task Recycle_Folder_SendsToBin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string folderPath = Path.Combine(vault, "Folder");
        Directory.CreateDirectory(folderPath);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, "Folder");

        LibraryResult<LibraryPath> result = await service.RecycleAsync(folder, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(folderPath, Assert.Single(fixture.RecycleBin.Sent));
        Assert.True(Directory.Exists(folderPath));
    }

    /// <summary>Recycling a protected item (Team/Project folder, Agent folder, its definition, its Work
    /// Dir, or a root) is refused with the matching Protected refusal text; the bin is never called.</summary>
    [Theory]
    [InlineData("TeamFolder")]
    [InlineData("ProjectFolder")]
    [InlineData("AgentFolder")]
    [InlineData("AgentDefinition")]
    [InlineData("WorkDir")]
    [InlineData("Root")]
    public async Task Recycle_Protected_Refused(string kind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateAgent("ada", "Ada", "ada");
        string workDir = Path.Combine(fixture.DataDir, "Agents", "ada", "work");
        Directory.CreateDirectory(workDir);
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        Directory.CreateDirectory(marketing);
        string projectFolder = Path.Combine(marketing, "Q4");
        Directory.CreateDirectory(projectFolder);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();

        (LibraryPath item, string expectedError, string sourcePath) = kind switch
        {
            "TeamFolder" => (fixture.ResolveTeams("Marketing"), "Team and Project folders can't be renamed, moved or deleted here.", marketing),
            "ProjectFolder" => (fixture.ResolveTeams("Marketing/Q4"), "Team and Project folders can't be renamed, moved or deleted here.", projectFolder),
            "AgentFolder" => (fixture.ResolveAgentsFolder("ada"), "Agent folders can't be renamed, moved or deleted here.", Path.Combine(fixture.DataDir, "Agents", "ada")),
            "AgentDefinition" => (fixture.ResolveAgents("ada"), "Agent folders can't be renamed, moved or deleted here.", Path.Combine(fixture.DataDir, "Agents", "ada", "ada.md")),
            "WorkDir" => (fixture.ResolveAgentsFolder("ada/work"), "Agent folders can't be renamed, moved or deleted here.", workDir),
            "Root" => (fixture.ResolveTeams(string.Empty), "A Library root can't be renamed, moved or deleted.", teamsRoot),
            _ => throw new InvalidOperationException($"Unknown kind: {kind}"),
        };

        LibraryResult<LibraryPath> result = await service.RecycleAsync(item, ct);

        Assert.Null(result.Value);
        Assert.Equal(expectedError, result.Error);
        Assert.Empty(fixture.RecycleBin.Sent);
        Assert.True(Directory.Exists(sourcePath) || File.Exists(sourcePath));
    }

    /// <summary>When the recycle bin refuses (unavailable), the caller sees the fixed settled refusal
    /// text (Spec §10 E-8) regardless of the bin's own error, and the file is left in place.</summary>
    [Fact]
    public async Task Recycle_Unavailable_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello");
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        fixture.RecycleBin.FailureError = "no recycle bin on this drive";
        LibraryPath file = fixture.Resolve(vault, "note.md");

        LibraryResult<LibraryPath> result = await service.RecycleAsync(file, ct);

        Assert.Null(result.Value);
        Assert.Equal("no recycle bin on this drive", result.Error);
        Assert.True(File.Exists(filePath));
    }

    /// <summary>Every <c>File.Delete(</c> allowed under the Library and Uploads source folders, with the number of
    /// call sites and why each is not a permanent delete of a Human document.</summary>
    private static readonly (string RelativePath, int Count, string Reason)[] AllowedFileDeletes =
    [
        // Library/UploadTempFile.cs: removes the hidden .{guid}.upload temp file of a refused or failed upload.
        ("Library/UploadTempFile.cs", 1, "temp file of a refused upload"),

        // Library/LibraryFileService.cs: DeleteUnsentUploadAsync drops an attachment chip's file that was never sent.
        ("Library/LibraryFileService.cs", 1, "an unsent attachment, not yet the Human's document"),

        // Library/AtomicFile.cs: cleans up the sibling temp file of an atomic write.
        ("Library/AtomicFile.cs", 1, "temp file of an atomic write"),

        // Library/LibraryRootStore.cs: resets the store's own settings file, never a Human document.
        ("Library/LibraryRootStore.cs", 1, "the root store's own settings file"),

        // Library/TeamFolderProvisioner.cs: removes the provisioner's own marker and temp files.
        ("Library/TeamFolderProvisioner.cs", 2, "the provisioner's own marker and temp files"),
    ];

    /// <summary>Source-text check (corrections-B4 item 15 note): the Library and Uploads source folders call
    /// <see cref="File.Delete(string)"/> only at the sites in <see cref="AllowedFileDeletes"/>, each documented, and
    /// never <see cref="Directory.Delete(string)"/>, so nothing is permanently, silently deleted from the code path
    /// — recycling always goes through <see cref="IRecycleBin"/>.</summary>
    [Fact]
    public void Recycle_NeverDeletesPermanently()
    {
        string repoRoot = RepoPaths.Root;
        string appRoot = Path.Combine(repoRoot, "src", "Huddle.App");
        List<string> files =
        [
            .. Directory.EnumerateFiles(Path.Combine(appRoot, "Library"), "*.cs", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(Path.Combine(appRoot, "Uploads"), "*.cs", SearchOption.AllDirectories),
        ];

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string relative = Path.GetRelativePath(appRoot, file).Replace('\\', '/');
            int allowed = AllowedFileDeletes.FirstOrDefault(entry => string.Equals(entry.RelativePath, relative, StringComparison.Ordinal)).Count;

            Assert.True(CssText.CountOccurrences(text, "File.Delete(") == allowed, $"{relative}: unexpected File.Delete( count (allowed {allowed}).");
            Assert.DoesNotContain("Directory.Delete(", text, StringComparison.Ordinal);
        }
    }

}
