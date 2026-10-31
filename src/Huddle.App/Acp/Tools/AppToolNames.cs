namespace Agency.Huddle.App.Acp.Tools;

/// <summary>How a tool's name looks once an Adapter has offered or reported it: bare, or behind an <c>mcp__{server}__</c> prefix.</summary>
internal static class AppToolNames
{
    /// <summary>Whether <paramref name="name"/> is the tool <paramref name="toolName"/> as offered.</summary>
    /// <param name="name">A tool name as offered or as an Adapter reported it.</param>
    /// <param name="toolName">The tool's bare name.</param>
    /// <returns><see langword="true"/> for the bare name and for any name ending <c>__</c> and the bare name.</returns>
    public static bool Matches(string name, string toolName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(toolName);

        return string.Equals(name, toolName, StringComparison.Ordinal)
            || name.EndsWith("__" + toolName, StringComparison.Ordinal);
    }
}
