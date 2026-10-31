using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Circuits;

namespace Agency.Huddle.Tests.Circuits;

/// <summary>Pins <see cref="CircuitConnectionTracker"/>: it follows the framework's connection callbacks, and one instance is both the circuit's handler and what components read.</summary>
public sealed class CircuitConnectionTrackerTests
{
    /// <summary>A circuit is connected until the framework says otherwise, so a callback that never arrives cannot silence a navigation that would have worked.</summary>
    [Fact]
    public void IsConnected_BeforeTheFrameworkReportsAnything_IsTrue()
    {
        CircuitConnectionTracker tracker = new();

        Assert.True(tracker.IsConnected);
    }

    /// <summary>The client going away turns the circuit disconnected and its coming back (a reconnect to the retained circuit) turns it connected again.</summary>
    [Fact]
    public async Task IsConnected_FollowsTheConnectionDownAndUpCallbacks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CircuitConnectionTracker tracker = new();

        await tracker.OnConnectionDownAsync(TestCircuit.Any(), ct);
        bool afterDown = tracker.IsConnected;
        await tracker.OnConnectionUpAsync(TestCircuit.Any(), ct);

        Assert.False(afterDown);
        Assert.True(tracker.IsConnected);
    }

    /// <summary>A circuit being disposed is disconnected: nobody can come back to it.</summary>
    [Fact]
    public async Task IsConnected_AfterTheCircuitCloses_IsFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        CircuitConnectionTracker tracker = new();

        await tracker.OnCircuitClosedAsync(TestCircuit.Any(), ct);

        Assert.False(tracker.IsConnected);
    }

    /// <summary>
    /// The framework reports the connection to the <see cref="CircuitHandler"/> it finds in the circuit's scope, and
    /// components read <see cref="ICircuitConnection"/> from the same scope, so the two must be one instance or what
    /// components read would stay connected for ever.
    /// </summary>
    [Fact]
    public async Task AddCircuitConnection_TheHandlerAndTheConnectionAreOneInstanceInAScope()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddCircuitConnection();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        CircuitHandler handler = Assert.Single(scope.ServiceProvider.GetServices<CircuitHandler>());
        ICircuitConnection connection = scope.ServiceProvider.GetRequiredService<ICircuitConnection>();
        await handler.OnConnectionDownAsync(TestCircuit.Any(), ct);

        Assert.Same(handler, connection);
        Assert.False(connection.IsConnected);
    }

    /// <summary>Each circuit has its own connection: one browser closing must not stop navigation in another's.</summary>
    [Fact]
    public async Task AddCircuitConnection_EachScopeHasItsOwnConnection()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddCircuitConnection();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope closing = provider.CreateScope();
        using IServiceScope other = provider.CreateScope();

        await closing.ServiceProvider.GetRequiredService<CircuitConnectionTracker>().OnConnectionDownAsync(TestCircuit.Any(), ct);

        Assert.False(closing.ServiceProvider.GetRequiredService<ICircuitConnection>().IsConnected);
        Assert.True(other.ServiceProvider.GetRequiredService<ICircuitConnection>().IsConnected);
    }
}
