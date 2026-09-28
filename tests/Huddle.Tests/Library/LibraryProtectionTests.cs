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
