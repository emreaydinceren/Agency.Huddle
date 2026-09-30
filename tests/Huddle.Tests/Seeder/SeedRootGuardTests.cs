using Agency.Huddle.Seeder;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>The wipe guard is what makes "start from a clean slate" safe, so each way it must refuse is pinned here.</summary>
public sealed class SeedRootGuardTests : IDisposable
{
    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "seed-guard-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Deletes the temp sandbox.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.sandbox))
        {
            Directory.Delete(this.sandbox, recursive: true);
        }
    }

    /// <summary>A drive root is never wiped.</summary>
    [Fact]
    public void Check_DriveRoot_IsRefused()
    {
        string? refusal = SeedRootGuard.Check(Path.GetPathRoot(this.sandbox)!, []);

        Assert.NotNull(refusal);
        Assert.Contains("drive root", refusal, StringComparison.Ordinal);
    }

    /// <summary>A folder directly under the drive root is too shallow to be a seed root.</summary>
    [Fact]
    public void Check_OneLevelBelowDriveRoot_IsRefused()
    {
        string shallow = Path.Combine(Path.GetPathRoot(this.sandbox)!, "seeds");

        string? refusal = SeedRootGuard.Check(shallow, []);

        Assert.NotNull(refusal);
        Assert.Contains("too close", refusal, StringComparison.Ordinal);
    }

    /// <summary>A folder that does not exist yet is fine: there is nothing to wipe.</summary>
    [Fact]
    public void Check_AbsentFolder_IsAllowed()
    {
        Assert.Null(SeedRootGuard.Check(Path.Combine(this.sandbox, "new"), []));
    }

    /// <summary>An empty existing folder is fine.</summary>
    [Fact]
    public void Check_EmptyFolder_IsAllowed()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "empty")).FullName;

        Assert.Null(SeedRootGuard.Check(root, []));
    }

    /// <summary>A non-empty folder without the marker is somebody else's data and is refused.</summary>
    [Fact]
    public void Check_NonEmptyFolderWithoutMarker_IsRefused()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "theirs")).FullName;
        File.WriteAllText(Path.Combine(root, "team.db"), "not yours");

        string? refusal = SeedRootGuard.Check(root, []);

        Assert.NotNull(refusal);
        Assert.Contains(SeedRootGuard.MarkerFileName, refusal, StringComparison.Ordinal);
    }

    /// <summary>A folder carrying the marker is a previous seed and may be wiped.</summary>
    [Fact]
    public void Check_NonEmptyFolderWithMarker_IsAllowed()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "mine")).FullName;
        File.WriteAllText(Path.Combine(root, SeedRootGuard.MarkerFileName), "x");
        File.WriteAllText(Path.Combine(root, "team.db"), "old seed");

        Assert.Null(SeedRootGuard.Check(root, []));
    }

    /// <summary>A folder inside a protected tree, such as the repository, is refused.</summary>
    [Fact]
    public void Check_InsideProtectedTree_IsRefused()
    {
        string repo = Path.Combine(this.sandbox, "repo");
        string inside = Path.Combine(repo, "src", "seed");

        string? refusal = SeedRootGuard.Check(inside, [new ProtectedFolder(repo, BlocksDescendants: true)]);

        Assert.NotNull(refusal);
        Assert.Contains("protected", refusal, StringComparison.Ordinal);
    }

    /// <summary>A folder inside a profile-style protected folder is allowed, because seeds may live under the profile or the temp folder.</summary>
    [Fact]
    public void Check_InsideProtectedAncestorOnlyFolder_IsAllowed()
    {
        string profile = Path.Combine(this.sandbox, "profile");
        string inside = Path.Combine(profile, "seeds", "huddle");

        Assert.Null(SeedRootGuard.Check(inside, [new ProtectedFolder(profile, BlocksDescendants: false)]));
    }

    /// <summary>A root that would contain a protected folder, such as the whole user profile, is refused.</summary>
    [Fact]
    public void Check_ContainingProtectedFolder_IsRefused()
    {
        string outer = Path.Combine(this.sandbox, "outer", "level");
        string profile = Path.Combine(outer, "profile");

        string? refusal = SeedRootGuard.Check(outer, [new ProtectedFolder(profile, BlocksDescendants: false)]);

        Assert.NotNull(refusal);
    }

    /// <summary>A link inside a marked seed root would make a recursive delete leave the root, so the root is refused.</summary>
    [Fact]
    public void Check_LinkInsideMarkedRoot_IsRefused()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "linked")).FullName;
        File.WriteAllText(Path.Combine(root, SeedRootGuard.MarkerFileName), "x");
        string target = Directory.CreateDirectory(Path.Combine(this.sandbox, "elsewhere")).FullName;
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(root, "escape"), target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Skip("Creating a symbolic link needs developer mode or elevation on this machine.");
            return;
        }

        string? refusal = SeedRootGuard.Check(root, []);

        Assert.NotNull(refusal);
        Assert.Contains("link", refusal, StringComparison.Ordinal);
    }

    /// <summary>Wipe removes a marked root and everything in it.</summary>
    [Fact]
    public void Wipe_MarkedRoot_DeletesIt()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "wipe-me", "deep")).FullName;
        File.WriteAllText(Path.Combine(root, SeedRootGuard.MarkerFileName), "x");
        File.WriteAllText(Path.Combine(root, "leftover.txt"), "old");

        SeedRootGuard.Wipe(root, []);

        Assert.False(Directory.Exists(root));
    }

    /// <summary>Wipe throws rather than deleting when the guard refuses.</summary>
    [Fact]
    public void Wipe_UnmarkedNonEmptyRoot_ThrowsAndKeepsTheFiles()
    {
        string root = Directory.CreateDirectory(Path.Combine(this.sandbox, "keep-me", "deep")).FullName;
        string file = Path.Combine(root, "precious.txt");
        File.WriteAllText(file, "keep");

        Assert.Throws<InvalidOperationException>(() => SeedRootGuard.Wipe(root, []));

        Assert.True(File.Exists(file));
    }
}
