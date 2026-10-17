namespace Agency.Huddle.App.Circuits;

/// <summary>
/// Whether the browser behind this circuit is still connected. A closed or reloaded tab does not end its
/// circuit at once: the server keeps it for the disconnected-circuit retention period in case the client
/// returns, and the singleton events its components subscribed to (<c>RoomEvents</c>, the Team catalog) keep
/// reaching them meanwhile. A handler that then talks to the client, <c>NavigationManager.NavigateTo</c> above
/// all, fails inside the framework with a <c>JSDisconnectedException</c> that no caller can catch. Scoped per
/// circuit.
/// </summary>
internal interface ICircuitConnection
{
    /// <summary>
    /// <see langword="false"/> only once the framework has reported the client gone. It is <see langword="true"/>
    /// before the first connection is reported, and where no circuit exists at all (prerendering), so a guard
    /// built on it holds back only a call that is known to fail.
    /// </summary>
    bool IsConnected { get; }
}
