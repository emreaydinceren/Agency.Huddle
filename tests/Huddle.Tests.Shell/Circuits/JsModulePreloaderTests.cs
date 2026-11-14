using Agency.Huddle.App.Circuits;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Agency.Huddle.Tests.Circuits;

/// <summary>
/// Pins <see cref="JsModulePreloader"/>: the circuit's handlers include it, and opening a circuit imports every app module
/// before anything renders, so a first-render call never waits for an import while its component is disposed.
/// </summary>
public sealed class JsModulePreloaderTests
{
    /// <summary>The circuit registrations put the preloader among the circuit's handlers, beside the connection tracker.</summary>
    [Fact]
    public void AddCircuitConnection_RegistersThePreloaderAsACircuitHandler()
    {
        ServiceCollection services = new();
        services.AddScoped<IJSRuntime, FakeModuleRuntime>();
        services.AddCircuitConnection();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IReadOnlyList<CircuitHandler> handlers = [.. scope.ServiceProvider.GetServices<CircuitHandler>()];

        Assert.Contains(handlers, static handler => handler is JsModulePreloader);
        Assert.Contains(handlers, static handler => handler is CircuitConnectionTracker);
    }

    /// <summary>Opening a circuit imports every module once, on that circuit's runtime.</summary>
    [Fact]
    public async Task OnCircuitOpenedAsync_ImportsEveryModule()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime js = new();
        JsModulePreloader preloader = new(js);

        await preloader.OnCircuitOpenedAsync(TestCircuit.Any(), ct);

        Assert.Equal(JsModule.All.Select(static module => module.Path).Order(StringComparer.Ordinal), js.Imports.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Imports that stall (a fetch that never answers) stop holding the circuit's first render back at the limit, and stay
    /// cached, so a later call gets the module once it arrives instead of importing it again.
    /// </summary>
    [Fact]
    public async Task OnCircuitOpenedAsync_ImportsStall_ReturnsAtTheLimitAndKeepsTheImports()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeModuleRuntime.ImportGate gate = new();
        FakeModuleRuntime js = new() { Gate = gate };
        JsModulePreloader preloader = new(js, TimeSpan.FromMilliseconds(50));

        Task opened = preloader.OnCircuitOpenedAsync(TestCircuit.Any(), ct);
        Task finished = await Task.WhenAny(opened, Task.Delay(TimeSpan.FromSeconds(30), ct));
        gate.Open();
        _ = await JsModule.Feed.ImportAsync(js, ct);

        Assert.Same(opened, finished);
        Assert.Equal(JsModule.All.Count, js.Imports.Count);
    }
}
