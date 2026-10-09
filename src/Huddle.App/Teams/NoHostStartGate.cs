namespace Agency.Huddle.App.Teams;

/// <summary>The start gate when no persona supervisor is registered (agents disabled): no host ever runs.</summary>
internal sealed class NoHostStartGate : IPersonaStartGate
{
    /// <inheritdoc />
    public Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)
    {
        return Task.FromResult(false);
    }
}
