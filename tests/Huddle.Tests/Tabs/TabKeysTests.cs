namespace Agency.Huddle.App.Tabs;

public sealed class TabKeysTests
{
    /// <summary>Each routed page maps to the key the specification's table gives it.</summary>
    [Theory]
    [InlineData("/", "chat")]
    [InlineData("/rooms/ab12", "room:ab12")]
    [InlineData("/teams/Core", "team:core")]
    [InlineData("/teams/Core/tasks", "team:core")]
    [InlineData("/teams/Core/projects/Alpha", "project:core/alpha")]
    [InlineData("/teams/Core/projects/Alpha/files", "project:core/alpha")]
    [InlineData("/teams/new", "team-new")]
    [InlineData("/teams/Core/setup", "team-setup:core")]
    [InlineData("/tasks", "tasks")]
    [InlineData("/tasks/board", "tasks:board")]
    [InlineData("/tasks/new", "task-new")]
    [InlineData("/tasks/item/7", "task:7")]
    [InlineData("/teammates", "teammates")]
    [InlineData("/settings", "settings")]
    [InlineData("/settings/appearance", "settings")]
    [InlineData("/library", "library")]
    [InlineData("/library?scopeRoot=a&scopePath=", "library")]
    [InlineData("/library?root=a&path=b.md", "library")]
    [InlineData("/something/else", "/something/else")]
    public void Describe_MapsUrlToKey(string url, string expectedKey)
    {
        Assert.Equal(expectedKey, TabKeys.Describe(url).Key);
    }

    /// <summary>An absolute address is reduced to its path, and a fragment is dropped.</summary>
    [Fact]
    public void ToRelative_AbsoluteWithFragment_KeepsPathAndQuery()
    {
        Assert.Equal("/rooms/ab?x=1", TabKeys.ToRelative("http://localhost:5000/rooms/ab?x=1#top"));
    }

    /// <summary>Only a single-slash path is a safe thing to remember.</summary>
    [Theory]
    [InlineData("/rooms/ab", true)]
    [InlineData("//evil", false)]
    [InlineData("https://evil", false)]
    [InlineData("\\evil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSafeRelative_AcceptsOnlyPaths(string? url, bool expected)
    {
        Assert.Equal(expected, TabKeys.IsSafeRelative(url));
    }
}
