using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>An <see cref="IRemoteAccessState"/> a test switches by hand.</summary>
internal sealed class FakeRemoteAccessState : IRemoteAccessState
{
    /// <summary>Creates the state.</summary>
    /// <param name="configured">Whether remote access is configured.</param>
    public FakeRemoteAccessState(bool configured = false) => this.IsConfigured = configured;

    /// <inheritdoc />
    public bool IsConfigured { get; set; }
}
