using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Circuits;

namespace Agency.Huddle.Tests.Ui;

/// <summary>An <see cref="ICircuitConnection"/> a test can flip, standing in for the framework's connection callbacks, which a bUnit context never raises.</summary>
internal sealed class FakeCircuitConnection : ICircuitConnection
{
    /// <summary>Whether the circuit's client is connected; true until a test sets it false, as a real circuit starts.</summary>
    public bool IsConnected { get; set; } = true;
}

/// <summary>Registers the circuit connection services a component that navigates from a server event needs.</summary>
internal static class CircuitConnectionTestExtensions
{
    /// <summary>
    /// Registers a <see cref="FakeCircuitConnection"/> and the real <see cref="ServerNavigator"/> over it, so a
    /// component that injects the navigator renders, and a test can drop the connection to see it hold back.
    /// </summary>
    /// <param name="services">The bUnit context's services.</param>
    /// <returns>The fake, connected until the test says otherwise.</returns>
    public static FakeCircuitConnection AddFakeCircuitConnection(this IServiceCollection services)
    {
        FakeCircuitConnection connection = new();
        services.AddLogging();
        services.AddSingleton<ICircuitConnection>(connection);
        services.AddScoped<ServerNavigator>();
        return connection;
    }
}
