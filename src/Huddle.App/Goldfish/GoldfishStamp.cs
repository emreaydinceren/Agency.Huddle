namespace Agency.Huddle.App.Goldfish;

/// <summary>The mark a goldfish's name carries where the Human sees it: "Ruth · goldfish".</summary>
internal static class GoldfishStamp
{
    /// <summary>The text appended to the Agent's name.</summary>
    internal const string Suffix = " · goldfish";

    /// <summary>The name of a goldfish.</summary>
    /// <param name="agentName">The Agent's name.</param>
    /// <returns>The name with <see cref="Suffix"/>.</returns>
    internal static string Apply(string agentName) => agentName + Suffix;

    /// <summary>The Agent's own name behind an author, stamped or not.</summary>
    /// <param name="author">The author as shown.</param>
    /// <returns>The author without <see cref="Suffix"/>.</returns>
    internal static string AgentName(string author) =>
        author.EndsWith(Suffix, StringComparison.Ordinal) ? author[..^Suffix.Length] : author;
}
