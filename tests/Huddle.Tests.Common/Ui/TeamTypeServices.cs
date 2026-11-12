using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Tests.Agents;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Registers a real <see cref="TeamTypeCatalog"/> for a component context whose sidebar names a Team's type.</summary>
internal static class TeamTypeServices
{
    /// <summary>
    /// Adds a <see cref="TeamTypeCatalog"/> over a fresh <see cref="TempDataDir"/> the context owns. It lists the one type
    /// <see cref="RoleFixtures"/> builds: id <c>web</c>, display name <c>Web</c>.
    /// </summary>
    /// <param name="services">The context's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTeamTypes(this IServiceCollection services)
    {
        services.AddSingleton(static _ => new OwnedTypesDir(new TempDataDir()));
        return services.AddSingleton(static provider => RoleFixtures.Types(provider.GetRequiredService<OwnedTypesDir>().Dir));
    }

    /// <summary>The data directory a context's <see cref="TeamTypeCatalog"/> reads; the container disposes it.</summary>
    /// <param name="Dir">The directory.</param>
    private sealed record OwnedTypesDir(TempDataDir Dir) : IDisposable
    {
        /// <summary>Deletes the directory.</summary>
        public void Dispose() => this.Dir.Dispose();
    }
}
