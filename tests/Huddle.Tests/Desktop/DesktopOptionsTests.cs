using Microsoft.Extensions.Configuration;
using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins <see cref="DesktopOptions"/> defaults and stop-token shape.</summary>
public sealed class DesktopOptionsTests
{
    /// <summary>Empty config binds to Enabled=false, OpenBrowser=true.</summary>
    [Fact]
    public void EmptyConfig_BindsToDefaults()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        DesktopOptions options = new();
        configuration.GetSection(DesktopOptions.SectionName).Bind(options);

        Assert.False(options.Enabled);
        Assert.True(options.OpenBrowser);
    }

    /// <summary>In-memory config binds both Enabled and OpenBrowser values.</summary>
    [Fact]
    public void InMemoryConfig_BindsBothValues()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Desktop:Enabled"] = "true",
                ["Team:Desktop:OpenBrowser"] = "false",
            })
            .Build();

        DesktopOptions options = new();
        configuration.GetSection(DesktopOptions.SectionName).Bind(options);

        Assert.True(options.Enabled);
        Assert.False(options.OpenBrowser);
    }

    /// <summary>SectionName is exactly "Team:Desktop".</summary>
    [Fact]
    public void SectionName_IsTeamDesktop()
    {
        Assert.Equal("Team:Desktop", DesktopOptions.SectionName);
    }

    /// <summary>StopToken matches base64url format with exactly 43 characters.</summary>
    [Fact]
    public void StopToken_MatchesBase64UrlFormat()
    {
        DesktopSession session = new();

        Assert.Matches(@"^[A-Za-z0-9_-]{43}$", session.StopToken);
    }

    /// <summary>Two sessions generate different stop tokens.</summary>
    [Fact]
    public void TwoSessions_HaveDifferentTokens()
    {
        DesktopSession session1 = new();
        DesktopSession session2 = new();

        Assert.NotEqual(session1.StopToken, session2.StopToken);
    }
}
