namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Marshals a store or hub event onto a component's renderer, tolerating a renderer that is already
/// gone. <c>ComponentBase.InvokeAsync</c> is <see langword="protected"/>, so a component passes it in
/// as a method group (<c>RendererDispatch.RunAsync(this.InvokeAsync, action)</c>) rather than this being
/// an extension method or a base class.
/// </summary>
internal static class RendererDispatch
{
    /// <summary>
    /// Runs <paramref name="action"/> through <paramref name="invokeAsync"/> (a component's
    /// <c>this.InvokeAsync</c>) and swallows the two exceptions that mean the renderer is gone. A store
    /// event can still fire while the component's circuit is winding down, and either exception then
    /// means there is nothing left to repaint: <see cref="ObjectDisposedException"/> when the circuit is
    /// gone, <see cref="InvalidOperationException"/> when the renderer was disposed mid-dispatch. Any
    /// other exception is a real fault in <paramref name="action"/> and propagates.
    /// </summary>
    /// <param name="invokeAsync">The component's <c>InvokeAsync(Func&lt;Task&gt;)</c>, as a method group.</param>
    /// <param name="action">The state update and repaint to run on the renderer.</param>
    /// <returns>A task that completes when the action has run or the renderer was found gone.</returns>
    public static async Task RunAsync(Func<Func<Task>, Task> invokeAsync, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(invokeAsync);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await invokeAsync(action);
        }
        catch (ObjectDisposedException)
        {
            // Circuit gone; nothing to render.
        }
        catch (InvalidOperationException)
        {
            // Renderer disposed mid-dispatch.
        }
    }
}
