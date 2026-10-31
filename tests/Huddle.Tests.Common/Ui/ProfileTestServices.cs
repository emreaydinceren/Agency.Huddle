using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Registers what the Settings Profile tab (<c>ProfilePanel</c>) injects, taken from a live
/// <see cref="TeamWebApplicationFactory"/>, so a bUnit context that renders the whole Settings page -
/// whose first tab is now Profile - resolves the very instances the factory's app runs.
/// </summary>
internal static class ProfileTestServices
{
    /// <summary>Adds the profile store, its <see cref="IHumanIdentity"/> view, the validator and the avatar store.</summary>
    /// <param name="services">The bUnit service collection.</param>
    /// <param name="factory">The factory whose services back them.</param>
    public static void AddProfile(IServiceCollection services, TeamWebApplicationFactory factory)
    {
        HumanProfileStore store = factory.Services.GetRequiredService<HumanProfileStore>();
        services.AddSingleton(store);
        services.AddSingleton<IHumanIdentity>(store);
        services.AddSingleton(factory.Services.GetRequiredService<HumanProfileValidator>());
        services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        AvatarLibraryPickSupport.RegisterUnconfigured(services);
    }
}
