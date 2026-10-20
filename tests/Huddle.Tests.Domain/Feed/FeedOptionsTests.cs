using Microsoft.Extensions.Configuration;
using Agency.Huddle.App;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins <see cref="FeedOptions"/> defaults and its binding from <c>Team:Feed</c> (spec 6.11).</summary>
public sealed class FeedOptionsTests
{
    /// <summary>Every key has the default the spec's table gives.</summary>
    [Fact]
    public void Defaults_MatchTheSpecTable()
    {
        FeedOptions options = new TeamOptions().Feed;

        Assert.True(options.Enabled);
        Assert.Equal(30, options.DismissedRetentionDays);
        Assert.Equal(30, options.ActivityRetentionDays);
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
        Assert.Equal(10000, options.QueueCapacity);
        Assert.Equal(200, options.BatchSize);
        Assert.Equal(50, options.PageSize);
        Assert.Equal(50, options.PanelLimit);
    }

    /// <summary>Configuration under <c>Team:Feed</c> binds onto <see cref="TeamOptions.Feed"/>.</summary>
    [Fact]
    public void Binding_TeamFeedSection_SetsEveryKey()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Feed:Enabled"] = "false",
                ["Team:Feed:DismissedRetentionDays"] = "7",
                ["Team:Feed:ActivityRetentionDays"] = "0",
                ["Team:Feed:CleanupInterval"] = "00:05:00",
                ["Team:Feed:QueueCapacity"] = "20",
                ["Team:Feed:BatchSize"] = "5",
                ["Team:Feed:PageSize"] = "9",
                ["Team:Feed:PanelLimit"] = "11",
            })
            .Build();
        TeamOptions options = new();

        configuration.GetSection("Team").Bind(options);

        Assert.False(options.Feed.Enabled);
        Assert.Equal(7, options.Feed.DismissedRetentionDays);
        Assert.Equal(0, options.Feed.ActivityRetentionDays);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Feed.CleanupInterval);
        Assert.Equal(20, options.Feed.QueueCapacity);
        Assert.Equal(5, options.Feed.BatchSize);
        Assert.Equal(9, options.Feed.PageSize);
        Assert.Equal(11, options.Feed.PanelLimit);
    }
}
