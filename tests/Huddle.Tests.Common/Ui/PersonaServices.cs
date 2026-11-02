using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Registers a real <see cref="PersonaStore"/> for a component context whose Room rows show a 1:1 Agent's title.</summary>
internal static class PersonaServices
{
    /// <summary>
    /// Adds a <see cref="PersonaStore"/> over <paramref name="dir"/>, or over a fresh <see cref="TempDataDir"/> the context
    /// owns when there is none, so a test never scans the real data folder. Both are built when first asked for and
    /// disposed with the context.
    /// </summary>
    /// <param name="services">The context's services.</param>
    /// <param name="dir">The test's data directory, or <see langword="null"/> for an empty one of the context's own.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddPersonaStore(this IServiceCollection services, TempDataDir? dir = null)
    {
        if (dir is not null)
        {
            return services.AddSingleton(_ => dir.CreatePersonaStore());
        }

        services.AddSingleton(static _ => new OwnedPersonaDir(new TempDataDir()));
        return services.AddSingleton(static provider => provider.GetRequiredService<OwnedPersonaDir>().Dir.CreatePersonaStore());
    }

    /// <summary>The empty data directory a context's <see cref="PersonaStore"/> scans when the test gave it none; the container disposes it.</summary>
    /// <param name="Dir">The directory.</param>
    private sealed record OwnedPersonaDir(TempDataDir Dir) : IDisposable
    {
        /// <summary>Deletes the directory.</summary>
        public void Dispose() => this.Dir.Dispose();
    }
}
