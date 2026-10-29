namespace Agency.Huddle.App.Teams;

/// <summary>Tells when a freshly hired Agent's host is running.</summary>
internal interface IPersonaStartGate
{
    /// <summary>Waits until the Agent has a running host or the timeout passes.</summary>
    /// <param name="name">The Agent name.</param>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns><see langword="true"/> when the Agent is running; <see langword="false"/> on timeout.</returns>
    Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct);
}
