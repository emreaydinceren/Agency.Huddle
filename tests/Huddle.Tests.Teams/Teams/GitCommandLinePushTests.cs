using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// <see cref="GitCommandLine.HasPush"/> and <see cref="GitCommandLine.OnlyLocalMergesIntoMain"/> (ADR-0087): any push is a push whatever
/// its arguments, and only a line that merges into main and pushes nothing counts as a local merge.
/// </summary>
public sealed class GitCommandLinePushTests
{
    /// <summary>A push of any shape is a push: a feature branch, a path, a URL, a bare push, <c>--all</c> and a push inside a shell wrapper.</summary>
    [Theory]
    [InlineData("git push origin feat/x")]
    [InlineData("git push ../repo main")]
    [InlineData("git push C:/x/bare.git")]
    [InlineData("git push")]
    [InlineData("git push --all")]
    [InlineData("git push https://example.com/r.git main")]
    [InlineData("bash -c \"git push\"")]
    public void HasPush_AnyPush_IsTrue(string command)
    {
        Assert.True(GitCommandLine.HasPush(command));
        Assert.False(GitCommandLine.OnlyLocalMergesIntoMain(command));
    }

    /// <summary>A merge or a pull of a feature branch is a local merge into main and not a push.</summary>
    [Theory]
    [InlineData("git merge feat/x")]
    [InlineData("git pull origin feat/x")]
    [InlineData("git merge --no-ff -m \"msg\" feat/x")]
    public void OnlyLocalMergesIntoMain_MergeOrPull_IsTrue(string command)
    {
        Assert.False(GitCommandLine.HasPush(command));
        Assert.True(GitCommandLine.OnlyLocalMergesIntoMain(command));
    }

    /// <summary>A line that merges and also pushes counts as a push.</summary>
    [Fact]
    public void OnlyLocalMergesIntoMain_MergeThenPush_IsFalse()
    {
        Assert.False(GitCommandLine.OnlyLocalMergesIntoMain("git merge feat/x && git push"));
        Assert.True(GitCommandLine.HasPush("git merge feat/x && git push"));
    }

    /// <summary>Taking main into a feature branch puts nothing on main, so it is not a local merge into main.</summary>
    [Fact]
    public void OnlyLocalMergesIntoMain_MergeOfMain_IsFalse()
    {
        Assert.False(GitCommandLine.OnlyLocalMergesIntoMain("git merge main"));
    }
}
