namespace Agency.Huddle.App.Library;

/// <summary>Tests for <see cref="LibraryProtection.For"/>.</summary>
public sealed class LibraryProtectionTests
{
    /// <summary>Different roles have different protection levels as per Spec §6.2, §6.4, §6.15, §6.16.</summary>
    [Theory]
    [MemberData(nameof(ProtectionTestData))]
    public void For_Role_HasExpectedProtection(LibraryNodeRole role, bool canRename, bool canMove, bool canDelete, string? reason)
    {
        LibraryPath path = CreatePath(role);
        LibraryProtection protection = LibraryProtection.For(path);

        Assert.Equal(canRename, protection.CanRename);
        Assert.Equal(canMove, protection.CanMove);
        Assert.Equal(canDelete, protection.CanDelete);
        Assert.Equal(reason, protection.Reason);
    }

    /// <summary>A folder that is the scope itself is protected like a root.</summary>
    [Fact]
    public void For_ScopeFolder_IsProtectedLikeRoot()
    {
        LibraryPath folderPath = CreatePath(LibraryNodeRole.Folder);
        LibraryProtection protection = LibraryProtection.For(folderPath, scope: folderPath);

        Assert.False(protection.CanRename);
        Assert.False(protection.CanMove);
        Assert.False(protection.CanDelete);
        Assert.Equal("A Library root can't be renamed, moved or deleted.", protection.Reason);
    }

    /// <summary>A file and a folder in the Uploads root are plain nodes: rename, move and delete are allowed.</summary>
    [Theory]
    [InlineData(LibraryNodeRole.File, "room1/photo.png")]
    [InlineData(LibraryNodeRole.Folder, "room1")]
    public void For_UploadsRootNode_IsUnprotected(LibraryNodeRole role, string relativePath)
    {
        LibraryRoot root = new("uploads", "Uploads", "/tmp/uploads", LibraryRootKind.Uploads);
        LibraryPath path = new(root, relativePath, "/tmp/uploads/" + relativePath, role);

        LibraryProtection protection = LibraryProtection.For(path);

        Assert.True(protection.CanRename);
        Assert.True(protection.CanMove);
        Assert.True(protection.CanDelete);
        Assert.Null(protection.Reason);
    }

    /// <summary>A read-only path (Skill Packs spec §6.11.3) can't be renamed, moved or deleted, whatever its role, and
    /// says why with <see cref="LibraryPath.ReadOnlyReason"/>.</summary>
    [Theory]
    [InlineData(LibraryNodeRole.File)]
    [InlineData(LibraryNodeRole.Folder)]
    public void For_ReadOnlyPath_AllowsNoWriteVerb(LibraryNodeRole role)
    {
        LibraryRoot root = new("skill-sources", "Skill Sources", "/tmp/skill-sources", LibraryRootKind.SkillSources, ReadOnly: true);
        LibraryPath path = new(root, "SKILLS.md", "/tmp/skill-sources/SKILLS.md", role, ReadOnly: true);

        LibraryProtection protection = LibraryProtection.For(path);

        Assert.False(protection.CanRename);
        Assert.False(protection.CanMove);
        Assert.False(protection.CanDelete);
        Assert.Equal(LibraryPath.ReadOnlyReason, protection.Reason);
    }

    public static IEnumerable<object?[]> ProtectionTestData()
    {
        yield return new object?[] { LibraryNodeRole.Root, false, false, false, "A Library root can't be renamed, moved or deleted." };
        yield return new object?[] { LibraryNodeRole.TeamFolder, false, false, false, "Team and Project folders can't be renamed, moved or deleted here." };
        yield return new object?[] { LibraryNodeRole.ProjectFolder, false, false, false, "Team and Project folders can't be renamed, moved or deleted here." };
        yield return new object?[] { LibraryNodeRole.TeammateFolder, false, false, false, "Teammate folders can't be renamed, moved or deleted here." };
        yield return new object?[] { LibraryNodeRole.TeammateDefinition, false, false, false, "Teammate folders can't be renamed, moved or deleted here." };
        yield return new object?[] { LibraryNodeRole.WorkDir, false, false, false, "Teammate folders can't be renamed, moved or deleted here." };
        yield return new object?[] { LibraryNodeRole.Folder, true, true, true, null };
        yield return new object?[] { LibraryNodeRole.File, true, true, true, null };
    }

    private static LibraryPath CreatePath(LibraryNodeRole role)
    {
        LibraryRoot root = new("root-id", "Teams", "/tmp/root", LibraryRootKind.Teams);
        return role switch
        {
            LibraryNodeRole.Root => new LibraryPath(root, "", "/tmp/root", role),
            LibraryNodeRole.TeamFolder => new LibraryPath(root, "Teams/Marketing", "/tmp/root/Teams/Marketing", role),
            LibraryNodeRole.ProjectFolder => new LibraryPath(root, "Teams/Marketing/Q4", "/tmp/root/Teams/Marketing/Q4", role),
            LibraryNodeRole.TeammateFolder => new LibraryPath(root, "Teammates/Nova", "/tmp/root/Teammates/Nova", role),
            LibraryNodeRole.TeammateDefinition => new LibraryPath(root, "Teammates/Nova/Nova.md", "/tmp/root/Teammates/Nova/Nova.md", role),
            LibraryNodeRole.WorkDir => new LibraryPath(root, "Teammates/Nova/work", "/tmp/root/Teammates/Nova/work", role),
            LibraryNodeRole.Folder => new LibraryPath(root, "Teams/Marketing/notes", "/tmp/root/Teams/Marketing/notes", role),
            LibraryNodeRole.File => new LibraryPath(root, "Teams/Marketing/plan.md", "/tmp/root/Teams/Marketing/plan.md", role),
            _ => throw new InvalidOperationException($"Unknown role: {role}"),
        };
    }
}
