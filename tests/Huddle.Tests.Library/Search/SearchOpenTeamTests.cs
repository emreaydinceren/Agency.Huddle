using Agency.Huddle.App.Search;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchOpenTeam.FromUrl"/>: the Team of the focused Tab, read from its address (spec §7.4 clause 1).</summary>
public sealed class SearchOpenTeamTests
{
    /// <summary>A Team's page, its Projects and its sub-tabs all name the same open Team; the rest name none.</summary>
    [Theory]
    [InlineData("/teams/Marketing", "Marketing")]
    [InlineData("/teams/Marketing/projects/Launch%20Q4", "Marketing")]
    [InlineData("/teams/Marketing/tasks?x=1", "Marketing")]
    [InlineData("/teams/Marketing%20Team/tasks", "Marketing Team")]
    [InlineData("/teams/new", null)]
    [InlineData("/rooms/abc", null)]
    [InlineData("http://localhost:5100/teams/Platform", "Platform")]
    public void FromUrl_TeamAddresses_ReturnTheTeam(string url, string? expected)
    {
        string? team = SearchOpenTeam.FromUrl(url);

        Assert.Equal(expected, team);
    }

    /// <summary>A missing address names no open Team.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromUrl_NullOrEmpty_ReturnsNull(string? url)
    {
        string? team = SearchOpenTeam.FromUrl(url);

        Assert.Null(team);
    }
}
