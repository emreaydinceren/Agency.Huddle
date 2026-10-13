using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>
/// Pins <see cref="WatchedFolderResolver.TryResolve"/>: every entry syntax and refusal reason in
/// FC §6.2-§6.3, and finding P-3's fifth reserved folder.
/// </summary>
public sealed class WatchedFolderResolverTests
{
    /// <summary>A Teammate Name resolves to their Work Dir under <c>{DataDir}/{Acp:WorkDir}</c>.</summary>
    [Fact]
    public void TryResolve_TeammateName_IsTheirWorkDir()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("nova", ["Nova"], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal("nova", folder.Entry);
        Assert.Equal(Path.Combine(dataDir.Path, "Teammates", "Nova", "work"), folder.FullPath);
    }

    /// <summary>A Teammate Name wins over a same-named folder directly under <c>DataDir</c>.</summary>
    [Fact]
    public void TryResolve_TeammateNameWinsOverSameNamedFolder()
    {
        using TempDataDir dataDir = new();
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Nova"));
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("Nova", ["Nova"], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(Path.Combine(dataDir.Path, "Teammates", "Nova", "work"), folder.FullPath);
    }

    /// <summary>A leading <c>./</c> or <c>.\</c> means the folder under <c>DataDir</c>, overriding a Teammate Name match.</summary>
    [Theory]
    [InlineData("./Nova")]
    [InlineData(".\\Nova")]
    public void TryResolve_DotSlashPrefix_MeansTheFolder(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, ["Nova"], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(Path.Combine(dataDir.Path, "Nova"), folder.FullPath);
    }

    /// <summary>A relative entry resolves under <c>DataDir</c> with either separator.</summary>
    [Theory]
    [InlineData("Shared/pricing")]
    [InlineData("Shared\\pricing")]
    public void TryResolve_RelativePath_BothSeparators(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(Path.Combine(dataDir.Path, "Shared", "pricing"), folder.FullPath);
    }

    /// <summary>A full path inside <c>DataDir</c> is accepted as itself.</summary>
    [Fact]
    public void TryResolve_FullPathInsideDataDir_Accepted()
    {
        using TempDataDir dataDir = new();
        string fullPath = Path.Combine(dataDir.Path, "Shared", "pricing");
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(fullPath, [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.Equal(fullPath, folder.FullPath);
    }

    /// <summary>A full path outside <c>DataDir</c> is refused, naming the resolved path and the <c>DataDir</c> folder name.</summary>
    [Fact]
    public void TryResolve_FullPathOutside_Refused()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));
        string outside = Path.Combine(Path.GetTempPath(), "team-tests-outside");

        bool resolved = resolver.TryResolve(outside, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        string appDataName = Path.GetFileName(dataDir.Path);
        Assert.Equal($"'{Path.GetFullPath(outside)}' is outside {appDataName}. Only folders inside it can be watched.", reason);
    }

    /// <summary>A relative entry that escapes <c>DataDir</c> with <c>..</c> is refused, the same as any other outside path.</summary>
    [Fact]
    public void TryResolve_DotDotEscape_Refused()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("Shared/../../x", [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.NotNull(reason);
        Assert.Contains("is outside", reason, StringComparison.Ordinal);
    }

    /// <summary>An entry naming <c>DataDir</c> itself, written as <c>.</c> or as the full path, is refused.</summary>
    [Theory]
    [InlineData(".")]
    public void TryResolve_DataDirItself_Refused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal($"'{entry}' is Huddle's own data folder, not a working folder.", reason);
    }

    /// <summary>The full <c>DataDir</c> path is refused the same way as <c>.</c>.</summary>
    [Fact]
    public void TryResolve_DataDirItselfAsFullPath_Refused()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(dataDir.Path, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal($"'{dataDir.Path}' is Huddle's own data folder, not a working folder.", reason);
    }

    /// <summary>Each of Huddle's own reserved folders is refused, including in other casing.</summary>
    [Theory]
    [InlineData("rooms")]
    [InlineData("logs")]
    [InlineData("file-state")]
    [InlineData("avatars")]
    [InlineData("room-sessions")]
    [InlineData("Rooms")]
    public void TryResolve_ReservedFolder_Refused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal($"'{entry}' holds Huddle's own data, not working files.", reason);
    }

    /// <summary>A subfolder of a reserved folder is refused too.</summary>
    [Fact]
    public void TryResolve_SubfolderOfReserved_Refused()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("file-state/x", [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("'file-state/x' holds Huddle's own data, not working files.", reason);
    }

    /// <summary>A blank entry is refused with a fixed reason.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_Blank_Refused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("A Watched Folder entry is blank.", reason);
    }

    /// <summary>A folder that does not exist yet still resolves: a missing folder is not a failure.</summary>
    [Fact]
    public void TryResolve_FolderDoesNotExist_StillResolves()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("Shared/not-there-yet", [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
        Assert.False(Directory.Exists(folder.FullPath));
    }

    /// <summary>Spec 6.8: the Uploads root and anything under it (including a Room's folder) is refused with the spec 7 text.</summary>
    [Theory]
    [InlineData("Uploads")]
    [InlineData("uploads")]
    [InlineData("./Uploads")]
    [InlineData("Uploads/room1")]
    [InlineData("Uploads\\room1\\deeper")]
    public void TryResolve_UploadsRootOrUnderIt_IsRefused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("Uploads are announced in the chat, not watched.", reason);
    }

    /// <summary>Spec 6.8: a full path at or under the Uploads root is refused too.</summary>
    [Fact]
    public void TryResolve_FullPathUnderUploadsRoot_IsRefused()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(Path.Combine(dataDir.Path, "Uploads", "room1"), [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("Uploads are announced in the chat, not watched.", reason);
    }

    /// <summary>Spec 6.8: a Team's Uploads folder and anything under it is refused with the same text, whichever case and separator.</summary>
    [Theory]
    [InlineData("./Teams/Alpha/Uploads")]
    [InlineData("./teams/alpha/UPLOADS/room1")]
    [InlineData("Teams/Alpha/uploads/room1/deeper")]
    [InlineData("Teams\\Alpha\\Uploads\\room1")]
    public void TryResolve_TeamUploadsOrUnderIt_IsRefused(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.False(resolved);
        Assert.Null(folder);
        Assert.Equal("Uploads are announced in the chat, not watched.", reason);
    }

    /// <summary>A Team's other folders, and a folder merely starting with <c>Uploads</c> inside a Team, still resolve.</summary>
    [Theory]
    [InlineData("./Teams/Alpha")]
    [InlineData("./Teams/Alpha/Docs")]
    [InlineData("./Teams/Alpha/Uploads-archive")]
    public void TryResolve_TeamFolderOutsideUploads_Accepted(string entry)
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve(entry, [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
    }

    /// <summary>A folder whose name merely starts with <c>Uploads</c> is not the Uploads root and still resolves.</summary>
    [Fact]
    public void TryResolve_SiblingNamedUploadsArchive_Accepted()
    {
        using TempDataDir dataDir = new();
        WatchedFolderResolver resolver = new(dataDir.Options(), new(dataDir.Options()));

        bool resolved = resolver.TryResolve("Uploads-archive", [], out WatchedFolder? folder, out string? reason);

        Assert.True(resolved);
        Assert.Null(reason);
        Assert.NotNull(folder);
    }

    /// <summary>
    /// Spec 6.8: Uploads is refused by its own check, never by <see cref="WatchedFolderResolver.ReservedFolders"/>,
    /// which the Library path resolver shares (the Library resolving a file in the Uploads root is pinned by
    /// <c>LibraryPathResolverTests.TryResolve_UploadsRoot_ResolvesFile</c>).
    /// </summary>
    [Fact]
    public void ReservedFolders_DoesNotContainUploads()
    {
        Assert.DoesNotContain("uploads", WatchedFolderResolver.ReservedFolders, StringComparer.OrdinalIgnoreCase);
    }
}
