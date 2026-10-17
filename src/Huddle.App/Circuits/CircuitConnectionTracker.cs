using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Agency.Huddle.App.Circuits;

/// <summary>
/// Follows the framework's connection callbacks for one circuit and answers <see cref="ICircuitConnection"/>
/// from them. It is the circuit's <see cref="CircuitHandler"/> and the <see cref="ICircuitConnection"/>
/// components read, one scoped instance in both roles
/// (<see cref="CircuitServiceCollectionExtensions.AddCircuitConnection(IServiceCollection)"/>). It starts connected and only ever
/// turns disconnected on the framework's word, so a missed callback can never silence a navigation that would
/// have worked. The callbacks arrive on arbitrary threads, hence the volatile flag.
/// </summary>
internal sealed class CircuitConnectionTracker : CircuitHandler, ICircuitConnection
{
    private volatile bool connected = true;

    /// <inheritdoc />
    public bool IsConnected => this.connected;

    /// <summary>The client connected, or reconnected to this retained circuit.</summary>
    /// <param name="circuit">The circuit, as the framework supplies it.</param>
    /// <param name="cancellationToken">Cancellation, as the framework supplies it.</param>
    /// <returns>A completed task.</returns>
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.connected = true;
        return Task.CompletedTask;
    }

    /// <summary>The client went away; the circuit stays for the retention period in case it returns.</summary>
    /// <param name="circuit">The circuit, as the framework supplies it.</param>
    /// <param name="cancellationToken">Cancellation, as the framework supplies it.</param>
    /// <returns>A completed task.</returns>
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.connected = false;
        return Task.CompletedTask;
    }

    /// <summary>The circuit is being disposed: nobody can return to it any more.</summary>
    /// <param name="circuit">The circuit, as the framework supplies it.</param>
    /// <param name="cancellationToken">Cancellation, as the framework supplies it.</param>
    /// <returns>A completed task.</returns>
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        this.connected = false;
        return Task.CompletedTask;
    }
}
