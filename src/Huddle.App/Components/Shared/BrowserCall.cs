using Microsoft.JSInterop;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Runs a JavaScript interop call that has nothing left to do when the circuit is already gone (a closed
/// tab, a reconnect that failed), so a component stops repeating the same empty
/// <see cref="JSDisconnectedException"/> catch. Only that exception is absorbed; a
/// <see cref="JSException"/> or anything else still reaches the caller. The overloads that take a <see cref="JsModule"/>
/// also absorb a <see cref="DotNetObjectReference{TValue}"/> argument disposed before the call (<see cref="IsGone"/>).
/// </summary>
internal static class BrowserCall
{
    /// <summary>How <see cref="ObjectDisposedException.ObjectName"/> starts for a disposed <see cref="DotNetObjectReference{TValue}"/>.</summary>
    private const string DotNetObjectReferenceName = "Microsoft.JSInterop.DotNetObjectReference`1";

    /// <summary>Runs <paramref name="call"/>, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="call">The interop call to run.</param>
    /// <returns><see langword="false"/> when the circuit was already gone (<see cref="JSDisconnectedException"/>), otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> TryAsync(Func<ValueTask> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            await call();
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Runs <paramref name="call"/> and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the call returns.</typeparam>
    /// <param name="call">The interop call to run.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> TryAsync<T>(Func<ValueTask<T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            return (true, await call());
        }
        catch (JSDisconnectedException)
        {
            return (false, default);
        }
    }

    // The overloads below hold the interop call and its catch together. The Blazor analyzer BL0016 only accepts a
    // try/catch around the call itself, so a call made inside the TryAsync lambda above is still reported.

    /// <summary>Calls a JavaScript function with no result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="identifier">The function to call, relative to <c>window</c>.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><see langword="false"/> when the circuit was already gone, otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> InvokeVoidAsync(IJSRuntime js, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            await js.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Calls a JavaScript function with no result and a cancellation token, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="identifier">The function to call, relative to <c>window</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><see langword="false"/> when the circuit was already gone, otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> InvokeVoidAsync(IJSRuntime js, string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            await js.InvokeVoidAsync(identifier, cancellationToken, args);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Calls a function on a JavaScript module or object with no result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="js">The module or object to call on.</param>
    /// <param name="identifier">The function to call, relative to <paramref name="js"/>.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><see langword="false"/> when the circuit was already gone, otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> InvokeVoidAsync(IJSObjectReference js, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            await js.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Calls a function exported by one of the app's modules with no result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="module">The module that exports the function.</param>
    /// <param name="identifier">The exported function's name.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><see langword="false"/> when the circuit was already gone, otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> InvokeVoidAsync(IJSRuntime js, JsModule module, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(module);
        try
        {
            IJSObjectReference reference = await module.ImportAsync(js);
            await reference.InvokeVoidAsync(identifier, args);
            return true;
        }
        catch (Exception ex) when (BrowserCall.IsGone(ex))
        {
            return false;
        }
    }

    /// <summary>Calls a function exported by one of the app's modules with no result and a cancellation token, treating a circuit that is already gone as "nothing to do".</summary>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="module">The module that exports the function.</param>
    /// <param name="identifier">The exported function's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><see langword="false"/> when the circuit was already gone, otherwise <see langword="true"/>.</returns>
    internal static async ValueTask<bool> InvokeVoidAsync(IJSRuntime js, JsModule module, string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(module);
        try
        {
            IJSObjectReference reference = await module.ImportAsync(js, cancellationToken);
            await reference.InvokeVoidAsync(identifier, cancellationToken, args);
            return true;
        }
        catch (Exception ex) when (BrowserCall.IsGone(ex))
        {
            return false;
        }
    }

    /// <summary>Calls a JavaScript function and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the function returns.</typeparam>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="identifier">The function to call, relative to <c>window</c>.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> InvokeAsync<T>(IJSRuntime js, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            return (true, await js.InvokeAsync<T>(identifier, args));
        }
        catch (JSDisconnectedException)
        {
            return (false, default);
        }
    }

    /// <summary>Calls a JavaScript function with a cancellation token and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the function returns.</typeparam>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="identifier">The function to call, relative to <c>window</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> InvokeAsync<T>(IJSRuntime js, string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            return (true, await js.InvokeAsync<T>(identifier, cancellationToken, args));
        }
        catch (JSDisconnectedException)
        {
            return (false, default);
        }
    }

    /// <summary>Calls a function on a JavaScript module or object and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the function returns.</typeparam>
    /// <param name="js">The module or object to call on.</param>
    /// <param name="identifier">The function to call, relative to <paramref name="js"/>.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> InvokeAsync<T>(IJSObjectReference js, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(js);

        try
        {
            return (true, await js.InvokeAsync<T>(identifier, args));
        }
        catch (JSDisconnectedException)
        {
            return (false, default);
        }
    }

    /// <summary>Calls a function exported by one of the app's modules and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the function returns.</typeparam>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="module">The module that exports the function.</param>
    /// <param name="identifier">The exported function's name.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> InvokeAsync<T>(IJSRuntime js, JsModule module, string identifier, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(module);
        try
        {
            IJSObjectReference reference = await module.ImportAsync(js);
            return (true, await reference.InvokeAsync<T>(identifier, args));
        }
        catch (Exception ex) when (BrowserCall.IsGone(ex))
        {
            return (false, default);
        }
    }

    /// <summary>Calls a function exported by one of the app's modules with a cancellation token and returns its result, treating a circuit that is already gone as "nothing to do".</summary>
    /// <typeparam name="T">The type the function returns.</typeparam>
    /// <param name="js">The runtime to call through.</param>
    /// <param name="module">The module that exports the function.</param>
    /// <param name="identifier">The exported function's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="args">The arguments, which must be JSON-serializable.</param>
    /// <returns><c>Ok</c> is <see langword="false"/> and <c>Value</c> is default when the circuit was already gone; otherwise <c>Ok</c> is <see langword="true"/> and <c>Value</c> is the result.</returns>
    internal static async ValueTask<(bool Ok, T? Value)> InvokeAsync<T>(IJSRuntime js, JsModule module, string identifier, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(module);
        try
        {
            IJSObjectReference reference = await module.ImportAsync(js, cancellationToken);
            return (true, await reference.InvokeAsync<T>(identifier, cancellationToken, args));
        }
        catch (Exception ex) when (BrowserCall.IsGone(ex))
        {
            return (false, default);
        }
    }

    /// <summary>
    /// Whether a module call has nobody left to serve: the circuit is gone, or a <see cref="DotNetObjectReference{TValue}"/>
    /// argument was disposed while the module was being imported. The first import on a circuit costs a round trip that a
    /// call to a global never had, and a component disposed during it (a redirect, a re-keyed Tab) has disposed its
    /// reference by the time the arguments are serialized. Any other <see cref="ObjectDisposedException"/> is a real fault.
    /// </summary>
    /// <param name="ex">The exception the call raised.</param>
    /// <returns><see langword="true"/> when the call can be dropped.</returns>
    internal static bool IsGone(Exception ex) =>
        ex is JSDisconnectedException
        || (ex is ObjectDisposedException disposed && disposed.ObjectName.StartsWith(DotNetObjectReferenceName, StringComparison.Ordinal));
}
