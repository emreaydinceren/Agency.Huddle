using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>A start gate that records the waits and answers with a configured outcome.</summary>
internal sealed class FakeStartGate : IPersonaStartGate
{
    /// <summary>Gets the names waited for, in order.</summary>
    public List<string> Waited { get; } = [];

    /// <summary>Gets or sets whether the Teammate reports running before the timeout.</summary>
    public bool Running { get; set; } = true;

    /// <summary>Gets or sets whether the wait throws <see cref="OperationCanceledException"/>.</summary>
    public bool Cancel { get; set; }

    /// <summary>Gets or sets a callback run at each wait with the waited name.</summary>
    public Action<string>? OnWait { get; set; }

    /// <inheritdoc />
    public Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct)
    {
        this.Waited.Add(name);
        this.OnWait?.Invoke(name);
        if (this.Cancel)
        {
            throw new OperationCanceledException();
        }

        return Task.FromResult(this.Running);
    }
}
