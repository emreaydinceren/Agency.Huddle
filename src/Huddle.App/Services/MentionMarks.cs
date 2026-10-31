using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

/// <summary>
/// The handles <see cref="MarkdownRenderer"/> draws as Mention pills in one Room's Message text: an <c>@</c>
/// followed by one of these, matched the way <see cref="MentionParser"/> matches it (longest first,
/// <see cref="StringComparison.OrdinalIgnoreCase"/>, ending at a word boundary), is wrapped in a
/// <c>mention</c> span.
/// </summary>
/// <param name="Handles">The Names and Aliases of the Room's Agents that are Members, and <see cref="MentionParser.AllHandle"/> when there is one.</param>
/// <param name="OwnHandles">The Human's own Name and Alias; a Mention of one also carries <c>mention--me</c>.</param>
public sealed record MentionMarks(IReadOnlyList<string> Handles, IReadOnlyList<string> OwnHandles)
{
    /// <summary>
    /// Builds one Room's handles from its Members and the alias source, as <see cref="MentionParser"/> builds its
    /// candidates, so a pill is drawn exactly where a Mention would be counted.
    /// </summary>
    /// <param name="members">The Room's Members.</param>
    /// <param name="humanName">The Human's current Name.</param>
    /// <param name="aliases">Every Alias currently known, library-wide.</param>
    /// <returns>The Room's mentionable handles.</returns>
    internal static MentionMarks For(IReadOnlyList<User> members, string humanName, IReadOnlyList<MentionAlias> aliases)
    {
        HashSet<string> agentNames = members
            .Where(static member => member.Kind == UserKind.Agent)
            .Select(static member => member.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> handles = [.. agentNames, .. aliases.Where(alias => agentNames.Contains(alias.Name)).Select(static alias => alias.Alias)];
        if (agentNames.Count > 0)
        {
            handles.Add(MentionParser.AllHandle);
        }

        List<string> ownHandles = [humanName, .. aliases.Where(alias => string.Equals(alias.Name, humanName, StringComparison.OrdinalIgnoreCase)).Select(static alias => alias.Alias)];
        return new MentionMarks(handles, ownHandles);
    }
}
