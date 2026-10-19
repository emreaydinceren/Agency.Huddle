namespace Agency.Huddle.App.Teams;

/// <summary>Tells when a freshly hired Teammate's host is running.</summary>
internal interface IPersonaStartGate
{
    /// <summary>Waits until the Teammate has a running host or the timeout passes.</summary>
    /// <param name="name">The Teammate name.</param>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns><see langword="true"/> when the Teammate is running; <see langword="false"/> on timeout.</returns>
    Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct);
}
