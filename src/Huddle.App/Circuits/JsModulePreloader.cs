using Agency.Huddle.App.Components.Shared;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.JSInterop;

namespace Agency.Huddle.App.Circuits;

/// <summary>
/// Imports every app module as soon as a circuit opens, before its components first render, so a component's first
/// call into a module finds the reference cached and runs without an extra round trip. Without it, a first-render call
/// waits for its import, and a component disposed in that wait (a redirect, a re-keyed Tab) hands the module a disposed
/// <see cref="DotNetObjectReference{TValue}"/> or an element that is no longer on the page.
/// </summary>
/// <remarks>
/// Blazor awaits <see cref="OnCircuitOpenedAsync"/> before it renders the circuit's root components, so the page is not
/// interactive until this returns. <see cref="WaitLimit"/> bounds that: a module whose fetch stalls (a slow proxy, a
/// remote link) stops holding the page back, and its first real call imports it on demand.
/// </remarks>
/// <param name="js">The circuit's runtime.</param>
/// <param name="waitLimit">How long the first render waits for the imports; <see langword="null"/> is <see cref="WaitLimit"/>.</param>
internal sealed class JsModulePreloader(IJSRuntime js, TimeSpan? waitLimit = null) : CircuitHandler
{
    /// <summary>How long a circuit's first render waits for the module imports.</summary>
    internal static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    /// <summary>Waits for the imports, up to the limit; failures and the limit are swallowed (<see cref="JsModule.PreloadAsync"/>).</summary>
    /// <param name="circuit">The circuit that opened.</param>
    /// <param name="cancellationToken">Cancels the wait (the connection went away).</param>
    /// <returns>A task that completes when every import has finished, failed, or run past the limit.</returns>
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(waitLimit ?? WaitLimit);
        await JsModule.PreloadAsync(js, limit.Token).ConfigureAwait(false);
    }
}
