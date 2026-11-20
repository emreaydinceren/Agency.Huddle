using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// Smart wakes spec 3.3: the Agent names a Task comment Mentions, after the guards. A Mention can only resolve to an Agent whose Persona is registered,
/// by Name or Alias; <c>@all</c> wakes no one; the author and the Human are left out; and nothing resolves while Tasks or wake-ups are off (read live).
/// It does not look at the Task's wake budget, which is a delivery matter.
/// </summary>
/// <param name="directory">Lists the Users a Mention can name.</param>
/// <param name="personas">Says which Agent Users are registered Personas, and supplies the Aliases.</param>
/// <param name="human">The Human's live identity, who is never woken.</param>
/// <param name="options">Read live for <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c>.</param>
internal sealed class MentionResolver(ITeamDirectory directory, PersonaStore personas, IHumanIdentity human, TeamOptions options)
{
    /// <summary>Resolves the Mentions of <paramref name="comment"/> to canonical Persona names.</summary>
    /// <param name="comment">The whole comment text, not the cut text a Message shows.</param>
    /// <param name="author">Who wrote it; never woken by their own comment.</param>
    /// <param name="ct">Cancels the directory read.</param>
    /// <returns>The Persona names in first-mention order, each once (ignoring case); empty when nobody is to be woken.</returns>
    public async Task<IReadOnlyList<string>> ResolveAsync(string comment, TaskActor author, CancellationToken ct)
    {
        if (!options.Tasks.Enabled || !options.Tasks.WakeEnabled || !comment.Contains('@', StringComparison.Ordinal))
        {
            return [];
        }

        IReadOnlyList<User> users = await directory.GetUsersAsync(ct).ConfigureAwait(false);

        // The parser expands @all to every Agent in the member list. Listing the registered Personas as non-Agent Users switches that expansion
        // off while their Names and Aliases still resolve, so a Persona literally called All is still named and @all beside it wakes no one else.
        List<User> members = [.. users
            .Where(user => user.Kind == UserKind.Agent && personas.Get(user.Name) is not null)
            .Select(static user => user with { Kind = UserKind.Human })];

        string authorName = GoldfishStamp.AgentName(author.Name);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> names = [];
        foreach (User hit in MentionParser.Parse(comment, members, personas.Aliases))
        {
            if (!string.Equals(hit.Name, authorName, StringComparison.OrdinalIgnoreCase) && !human.Matches(hit.Name) && seen.Add(hit.Name))
            {
                names.Add(hit.Name);
            }
        }

        return names;
    }
}
