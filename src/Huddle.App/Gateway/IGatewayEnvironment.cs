namespace Agency.Huddle.App.Gateway;

/// <summary>The seam over process environment variables, so tests never mutate the real environment.</summary>
internal interface IGatewayEnvironment
{
    /// <summary>Returns the variable's value, or <see langword="null"/> when it is not set.</summary>
    /// <param name="name">The variable name, e.g. <c>HUDDLE_GATEWAY_TOKEN</c>.</param>
    string? Get(string name);
}
