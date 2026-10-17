using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Turns a saved Teammate into what a <see cref="ProfileCard"/> draws, for the avatar profile popover.
/// It reads the same sources Build a team does for a member (the Persona file, its role, the hiring
/// pool's job) except that the role comes from the Persona's own frontmatter rather than from a team
/// type the Human has selected.
/// </summary>
/// <param name="personas">The Teammate directory and definitions.</param>
/// <param name="types">The team types, to find the Persona's role.</param>
/// <param name="pool">The hiring pool, for the role's job description and tier.</param>
/// <param name="avatars">The avatar store.</param>
/// <param name="teams">The Team catalog, for who manages a Team and who belongs to it.</param>
/// <param name="directory">The user directory, for the Teammate's agent user.</param>
/// <param name="gateway">The pipe gateway, for whether the agent is connected.</param>
/// <param name="health">Reported health, the other input to the status dot.</param>
/// <param name="human">The Human's live identity, so the Human's own avatar opens their profile too; <see langword="null"/> treats every name as a Teammate's.</param>
internal sealed class TeammateProfiles(
    PersonaStore personas,
    TeamTypeCatalog types,
    HiringPoolCatalog pool,
    AvatarStore avatars,
    ITeamCatalog teams,
    ITeamDirectory directory,
    IAgentGateway gateway,
    PersonaHealth health,
    IHumanIdentity? human = null)
{
    /// <summary>Builds the profile of the Teammate - or the Human - called <paramref name="name"/>.</summary>
    /// <param name="name">The Teammate's Name or alias.</param>
    /// <param name="team">The Team the avatar was seen in, or <see langword="null"/> outside any Team.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The profile, or <see langword="null"/> when no Persona resolves for <paramref name="name"/> (a raw pipe client, a demo agent).</returns>
    public async Task<TeammateProfileView?> BuildAsync(string name, string? team, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (human is not null && human.Matches(name))
        {
            HumanProfile me = human.Current;
            TeammateProfile humanProfile = new(me.Name, me.Title ?? string.Empty, me.Description ?? string.Empty, avatars.Get(me.Name));
            return new TeammateProfileView(humanProfile, Manager: false, ProfileMembership.None, Agent: null, team) { IsHuman = true };
        }

        if (personas.ResolveByNameOrAlias(name) is not { } entry || personas.Get(entry.Name) is not { } persona)
        {
            return null;
        }

        PersonaIdentity? identity = PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? read, out _) ? read : null;
        RoleSpec? role = null;
        if (identity?.Role is { } roleRef && types.Find(roleRef.TypeId) is { } type)
        {
            role = type.Roles.FirstOrDefault(candidate => string.Equals(candidate.Id, roleRef.RoleId, StringComparison.Ordinal));
        }

        PoolJob? job = role is null ? null : pool.FindJob(role.Job ?? role.Id);
        User? agent = await directory.FindUserByNameAsync(entry.Name, ct);
        bool connected = agent is not null && gateway.IsOnline(agent.Id);
        PersonaState state = PersonaStatusResolver.Resolve(connected, health.Get(entry.Name)).State;

        TeamSummary? summary = team is null ? null : teams.Find(team);
        bool manager = summary?.Manager is { } managerName && string.Equals(managerName, entry.Name, StringComparison.OrdinalIgnoreCase);
        bool member = summary is not null && summary.Members.Any(candidate => string.Equals(candidate, entry.Name, StringComparison.OrdinalIgnoreCase));

        TeammateProfile profile = new(entry.Name, entry.Title, job?.Description ?? string.Empty, avatars.Get(entry.Name))
        {
            State = state,
            Tier = job?.Model.Tier ?? role?.Model?.Tier,
            SkillNames = identity?.Skills ?? [],
        };
        return new TeammateProfileView(profile, manager, member ? ProfileMembership.Member : ProfileMembership.None, agent, team);
    }
}

/// <summary>A Teammate's profile card data with what the popover needs around it.</summary>
/// <param name="Profile">What the card draws.</param>
/// <param name="Manager">Whether the Teammate manages the Team it was seen in.</param>
/// <param name="Membership">Whether the Teammate belongs to the Team it was seen in.</param>
/// <param name="Agent">The Teammate's agent user, or <see langword="null"/> when it has never connected (so there is no Room to message it in).</param>
/// <param name="Team">The Team the avatar was seen in, or <see langword="null"/> for a root Room.</param>
internal sealed record TeammateProfileView(TeammateProfile Profile, bool Manager, ProfileMembership Membership, User? Agent, string? Team)
{
    /// <summary>The agent user's id, or <see langword="null"/> when it has never connected.</summary>
    public string? AgentId => this.Agent?.Id;

    /// <summary>Whether this is the Human's own profile, edited on the Settings Profile tab rather than on a Teammate card.</summary>
    public bool IsHuman { get; init; }
}
