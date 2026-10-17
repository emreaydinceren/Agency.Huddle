using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Circuits;

/// <summary>
/// Navigates from a handler that a server event, not the Human, started: a store or hub event, or whatever
/// follows an <c>await</c> that the Human's click is no longer waiting on. Such a handler can run after the
/// browser has gone, while the circuit is still retained, and <c>NavigationManager.NavigateTo</c> then fails
/// inside the framework, in a fire-and-forget call no one can catch, and logs two errors ("Navigation
/// failed…" and "Unhandled exception in circuit") for a navigation nobody could have seen. This skips the call
/// instead. A handler running for the Human's own click, key press or script callback does not need it: the
/// client was there when it arrived, so it calls <c>NavigateTo</c> directly. Scoped per circuit.
/// </summary>
/// <param name="navigation">The circuit's navigation manager.</param>
/// <param name="connection">Whether the circuit's client is still connected.</param>
/// <param name="logger">Where a skipped navigation is noted.</param>
internal sealed class ServerNavigator(NavigationManager navigation, ICircuitConnection connection, ILogger<ServerNavigator> logger)
{
    /// <summary>
    /// Navigates to <paramref name="uri"/> unless the client is gone. A skipped navigation is not replayed if
    /// the client reconnects: what the handler decided is already in the app's state, and the Human's next
    /// click navigates as usual.
    /// </summary>
    /// <param name="uri">The address to go to, as <c>NavigationManager.NavigateTo</c> takes it.</param>
    /// <returns><see langword="true"/> when the navigation was issued; <see langword="false"/> when the client is disconnected and it was skipped.</returns>
    public bool NavigateTo(string uri)
    {
        if (!connection.IsConnected)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Did not navigate to {Uri}: the circuit's client is disconnected.", uri);
            }

            return false;
        }

        navigation.NavigateTo(uri);
        return true;
    }
}
