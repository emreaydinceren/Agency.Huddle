namespace Agency.Huddle.App.Gateway;

/// <summary>Reads the real process environment.</summary>
internal sealed class ProcessGatewayEnvironment : IGatewayEnvironment
{
    /// <inheritdoc/>
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
