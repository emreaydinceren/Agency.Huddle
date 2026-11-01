using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins <see cref="PathContainment"/>: one answer to "is this path inside that folder?" for every caller,
/// and the one slash-normalised relative path.
/// </summary>
public sealed class PathContainmentTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pc-root", "b");

    /// <summary>A file below the root is inside whether or not the root counts.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsInside_ChildOfRoot_IsInside(bool includeRoot)
    {
        Assert.True(PathContainment.IsInside(Path.Combine(Root, "x", "y.md"), Root, includeRoot, PathContainment.Comparison));
    }

    /// <summary>The root itself is inside only when asked.</summary>
    [Fact]
    public void IsInside_RootItself_FollowsIncludeRoot()
    {
        Assert.True(PathContainment.IsInside(Root, Root, includeRoot: true, PathContainment.Comparison));
        Assert.False(PathContainment.IsInside(Root, Root, includeRoot: false, PathContainment.Comparison));
    }

    /// <summary>A sibling whose name merely starts with the root's name is not inside.</summary>
    [Fact]
    public void IsInside_SiblingSharingNamePrefix_IsNotInside()
    {
        string sibling = Root + "c";

        Assert.False(PathContainment.IsInside(Path.Combine(sibling, "x.md"), Root, includeRoot: true, PathContainment.Comparison));
        Assert.False(PathContainment.IsInside(sibling, Root, includeRoot: true, PathContainment.Comparison));
    }

    /// <summary>A trailing separator on the root or on the path changes nothing.</summary>
    [Fact]
    public void IsInside_TrailingSeparators_AreIgnored()
    {
        string rootWithSeparator = Root + Path.DirectorySeparatorChar;

        Assert.True(PathContainment.IsInside(Path.Combine(Root, "x.md"), rootWithSeparator, includeRoot: false, PathContainment.Comparison));
        Assert.True(PathContainment.IsInside(Root, rootWithSeparator, includeRoot: true, PathContainment.Comparison));
        Assert.False(PathContainment.IsInside(rootWithSeparator, Root, includeRoot: false, PathContainment.Comparison));
    }

    /// <summary>A path that climbs out of the root with <c>..</c> is not inside, and one that climbs back in is.</summary>
    [Fact]
    public void IsInside_DotDotSegments_AreResolved()
    {
        Assert.False(PathContainment.IsInside(Path.Combine(Root, "..", "other", "x.md"), Root, includeRoot: true, PathContainment.Comparison));
        Assert.False(PathContainment.IsInside(Path.Combine(Root, "..", "b" + "c", "x.md"), Root, includeRoot: true, PathContainment.Comparison));
        Assert.True(PathContainment.IsInside(Path.Combine(Root, "x", "..", "y.md"), Root, includeRoot: false, PathContainment.Comparison));
    }

    /// <summary>A filesystem or drive root contains everything below it and is itself the root.</summary>
    [Fact]
    public void IsInside_FilesystemRoot_ContainsItsChildren()
    {
        string fsRoot = Path.GetPathRoot(Root) ?? throw new InvalidOperationException("The temp path has no root.");

        Assert.True(PathContainment.IsInside(Root, fsRoot, includeRoot: false, PathContainment.Comparison));
        Assert.True(PathContainment.IsInside(fsRoot, fsRoot, includeRoot: true, PathContainment.Comparison));
        Assert.False(PathContainment.IsInside(fsRoot, fsRoot, includeRoot: false, PathContainment.Comparison));
    }

    /// <summary>A path that differs from the root only in letter case is inside exactly when the platform ignores case.</summary>
    [Fact]
    public void IsInside_DifferentCase_FollowsPlatformComparison()
    {
        string shouted = Path.Combine(Path.GetTempPath(), "PC-ROOT", "B", "x.md");

        bool expected = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase;

        Assert.Equal(expected, PathContainment.IsInside(shouted, Root, includeRoot: false, PathContainment.Comparison));
    }

    /// <summary>A refusal guard that asks to ignore case matches a differently cased path on every platform, Linux included.</summary>
    [Fact]
    public void IsInside_IgnoreCaseRequested_MatchesDifferentCaseOnEveryPlatform()
    {
        string shouted = Path.Combine(Path.GetTempPath(), "PC-ROOT", "B", "x.md");

        Assert.True(PathContainment.IsInside(shouted, Root, includeRoot: false, StringComparison.OrdinalIgnoreCase));
        Assert.False(PathContainment.IsInside(shouted, Root, includeRoot: false, StringComparison.Ordinal));
    }

    /// <summary>
    /// The part below the root keeps the path's own casing and is sliced under the requested comparison, so a
    /// case-insensitive match never turns into a <c>..</c> path the way <see cref="Path.GetRelativePath(string, string)"/> would on Linux.
    /// </summary>
    [Fact]
    public void Below_IgnoreCaseMatch_ReturnsTheRemainderInTheCallersCasing()
    {
        string shouted = Path.Combine(Path.GetTempPath(), "PC-ROOT", "B", "Alpha", "UPLOADS");

        string? below = PathContainment.Below(shouted, Root, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(Path.Combine("Alpha", "UPLOADS"), below);
    }

    /// <summary>The root itself is an empty remainder, and a path outside the root has none.</summary>
    [Fact]
    public void Below_RootAndOutside_AreEmptyAndNull()
    {
        Assert.Equal(string.Empty, PathContainment.Below(Root + Path.DirectorySeparatorChar, Root, StringComparison.Ordinal));
        Assert.Null(PathContainment.Below(Root + "c", Root, StringComparison.Ordinal));
    }

    /// <summary>The relative path uses forward slashes whatever the platform separator is.</summary>
    [Fact]
    public void RelativeSlashPath_NestedFile_UsesForwardSlashes()
    {
        string file = Path.Combine(Root, "sub", "deep", "f.md");

        Assert.Equal("sub/deep/f.md", PathContainment.RelativeSlashPath(Root, file));
    }
}
