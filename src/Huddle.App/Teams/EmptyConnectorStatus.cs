namespace Agency.Huddle.App.Teams;

/// <summary>A connector status that reports nothing connected.</summary>
internal sealed class EmptyConnectorStatus : IConnectorStatus
{
    /// <inheritdoc />
    public string? Location => null;

    /// <inheritdoc />
    public bool IsConnected(string connector) => false;
}
