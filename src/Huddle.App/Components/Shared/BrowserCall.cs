using Microsoft.JSInterop;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Runs a JavaScript interop call that has nothing left to do when the circuit is already gone (a closed
/// tab, a reconnect that failed), so a component stops repeating the same empty
/// <see cref="JSDisconnectedException"/> catch. Only that exception is absorbed; a
/// <see cref="JSException"/> or anything else still reaches the caller.
/// </summary>
internal static class BrowserCall
{
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
}
