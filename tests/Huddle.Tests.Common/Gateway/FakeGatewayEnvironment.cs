using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>An <see cref="IGatewayEnvironment"/> backed by a dictionary, so tests never touch the process environment.</summary>
internal sealed class FakeGatewayEnvironment : IGatewayEnvironment
{
    private readonly Dictionary<string, string> variables = new(StringComparer.Ordinal);

    /// <summary>Sets a variable and returns this instance.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">Its value.</param>
    public FakeGatewayEnvironment With(string name, string value)
    {
        this.variables[name] = value;
        return this;
    }

    /// <summary>Removes a variable.</summary>
    /// <param name="name">The variable name.</param>
    public void Remove(string name) => this.variables.Remove(name);

    /// <inheritdoc />
    public string? Get(string name) => this.variables.TryGetValue(name, out string? value) ? value : null;
}
