using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Agency.Huddle.App.Circuits;

/// <summary>Registers the circuit connection tracking.</summary>
internal static class CircuitServiceCollectionExtensions
{
    /// <summary>
    /// Registers one scoped <see cref="CircuitConnectionTracker"/> under three names: the framework finds it as a
    /// <see cref="CircuitHandler"/> in the circuit's scope and reports the connection to it, components read it
    /// as an <see cref="ICircuitConnection"/>, and <see cref="ServerNavigator"/> is built on that. The three
    /// resolve to one instance. Two instances would leave the one components read forever connected, and no
    /// compiler or runtime error would say so.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCircuitConnection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<CircuitConnectionTracker>();
        services.AddScoped<ICircuitConnection>(static provider => provider.GetRequiredService<CircuitConnectionTracker>());
        services.AddScoped<CircuitHandler>(static provider => provider.GetRequiredService<CircuitConnectionTracker>());
        services.AddScoped<ServerNavigator>();
        return services;
    }
}
