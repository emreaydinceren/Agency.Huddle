using Agency.Huddle.App;
using Agency.Huddle.App.References;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.References;

/// <summary>Pins <see cref="ReferencesOptions"/>' defaults, binding and the 200-character floor, per spec §10 and D-20.</summary>
public sealed class ReferencesOptionsTests
{
    /// <summary>The defaults match spec §10: the header is expanded and capped at 2000 characters.</summary>
    [Fact]
    public void Defaults()
    {
        ReferencesOptions options = new();

        Assert.True(options.ExpandInPrompt);
        Assert.Equal(2000, options.MaxPromptChars);
        Assert.Equal(2000, options.EffectiveMaxPromptChars);
    }

    /// <summary>Both keys bind from <c>Team:References</c> onto <see cref="TeamOptions.References"/>.</summary>
    [Fact]
    public void Binds_FromTeamReferences()
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["Team:References:ExpandInPrompt"] = "false",
            ["Team:References:MaxPromptChars"] = "500",
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);

        Assert.False(teamOptions.References.ExpandInPrompt);
        Assert.Equal(500, teamOptions.References.EffectiveMaxPromptChars);
    }

    /// <summary>A configured <c>MaxPromptChars</c> below 200 is raised to 200, and 200 itself is kept.</summary>
    [Fact]
    public void MaxPromptChars_Below200_Raised()
    {
        ReferencesOptions low = new() { MaxPromptChars = 50 };
        ReferencesOptions floor = new() { MaxPromptChars = 200 };

        Assert.Equal(200, low.EffectiveMaxPromptChars);
        Assert.Equal(200, floor.EffectiveMaxPromptChars);
    }
}
