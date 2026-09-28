using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Covers <see cref="AcpOptions.TeammatesDir"/> (Spec §6.15, §7) and the start-up guard that
/// rejects the retired <c>Team:Acp:TeamsDir</c> configuration key, mirroring the existing
/// <c>Acp:PersonaDir</c> guard in <see cref="ServiceCollectionExtensionsTests"/>.
/// </summary>
public sealed class TeammatesDirOptionTests
{
    /// <summary>The default <see cref="AcpOptions.TeammatesDir"/> is <c>Teammates</c>.</summary>
    [Fact]
    public void Default_TeammatesDir_IsTeammates()
    {
        AcpOptions options = new();

        Assert.Equal("Teammates", options.TeammatesDir);
    }

    /// <summary>
    /// Setting the retired <c>Team:Acp:TeamsDir</c> key throws at start-up naming the new
    /// <c>Team:Acp:TeammatesDir</c> key, the same shape as the <c>Acp:PersonaDir</c> guard.
    /// </summary>
    [Fact]
    public void AddTeam_WithAcpTeamsDir_Throws()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:TeamsDir"] = "Teams",
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => services.AddTeamServices(configuration));

        Assert.Equal(
            "Configuration key 'Team:Acp:TeamsDir' was renamed to 'Team:Acp:TeammatesDir'. " +
            "Update the configuration source that sets it (environment variable, user secret, etc.) " +
            "- there is no automatic fallback.",
            ex.Message);
    }

    /// <summary>
    /// The negative case: configuration that never mentions the retired key composes normally,
    /// so the guard above is not a false positive for every other test that calls
    /// <see cref="ServiceCollectionExtensions.AddTeamServices"/> without setting it.
    /// </summary>
    [Fact]
    public void AddTeam_WithoutTeamsDir_DoesNotThrow()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();

        IServiceCollection result = services.AddTeamServices(configuration);

        Assert.Same(services, result);
    }
}
