using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Turns a saved Agent into what a <see cref="ProfileCard"/> draws, for the avatar profile popover.
/// It reads the same sources Build a team does for a member (the Persona file, its role, the hiring
/// pool's job) except that the role comes from the Persona's own frontmatter rather than from a team
/// type the Human has selected. It also reads out what the Agent runs on: its Adapter from the
/// Persona file, and its Model and Effort from their stores.
/// </summary>
/// <param name="personas">The Agent directory and definitions.</param>
/// <param name="types">The team types, to find the Persona's role.</param>
/// <param name="pool">The hiring pool, for the role's job description and tier.</param>
/// <param name="avatars">The avatar store.</param>
/// <param name="teams">The Team catalog, for who manages a Team and who belongs to it.</param>
/// <param name="directory">The user directory, for the Agent's agent user.</param>
/// <param name="gateway">The pipe gateway, for whether the agent is connected.</param>
/// <param name="health">Reported health, the other input to the status dot.</param>
/// <param name="adapters">The configured Adapters, for the display name of the one the Agent runs on.</param>
/// <param name="human">The Human's live identity, so the Human's own avatar opens their profile too; <see langword="null"/> treats every name as an Agent's.</param>
internal sealed class AgentProfiles(
    PersonaStore personas,
    TeamTypeCatalog types,
    HiringPoolCatalog pool,
    AvatarStore avatars,
    ITeamCatalog teams,
    ITeamDirectory directory,
    IAgentGateway gateway,
    PersonaHealth health,
    AdapterCatalog adapters,
    IHumanIdentity? human = null)
{
    /// <summary>What the Agent card says for a Model the Persona has not chosen.</summary>
    private const string DefaultModelText = "Agent default";

    /// <summary>What the Agent card says for an Effort the Persona has not chosen.</summary>
    private const string DefaultEffortText = "Model default";

    /// <summary>Builds the profile of the Agent - or the Human - called <paramref name="name"/>.</summary>
    /// <param name="name">The Agent's Name or alias.</param>
    /// <param name="team">The Team the avatar was seen in, or <see langword="null"/> outside any Team.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The profile, or <see langword="null"/> when no Persona resolves for <paramref name="name"/> (a raw pipe client, a demo agent).</returns>
    public async Task<AgentProfileView?> BuildAsync(string name, string? team, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (human is not null && human.Matches(name))
        {
            HumanProfile me = human.Current;
            AgentProfile humanProfile = new(me.Name, me.Title ?? string.Empty, me.Description ?? string.Empty, avatars.Get(me.Name));
            return new AgentProfileView(humanProfile, Manager: false, ProfileMembership.None, Agent: null, team) { IsHuman = true };
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

        AgentProfile profile = new(entry.Name, entry.Title, job?.Description ?? string.Empty, avatars.Get(entry.Name))
        {
            State = state,
            Runtime = this.ReadRuntime(persona),
            Tier = job?.Model.Tier ?? role?.Model?.Tier,
            SkillNames = identity?.Skills ?? [],
        };
        return new AgentProfileView(profile, manager, member ? ProfileMembership.Member : ProfileMembership.None, agent, team);
    }

    /// <summary>
    /// Reads out what the Persona runs on. The Adapter is the one it actually launches on: the default
    /// when the Persona names none or names one that is not configured (the Agent card warns of the
    /// same fallback). Model and Effort stay raw ids, because naming them would need a probe.
    /// </summary>
    /// <param name="persona">The Persona with its stored Model and Effort joined in.</param>
    /// <returns>The three display strings.</returns>
    private ProfileRuntime ReadRuntime(Persona persona) => new(
        (adapters.Find(persona.Adapter) ?? adapters.Default).DisplayName,
        persona.Model ?? DefaultModelText,
        persona.Effort ?? DefaultEffortText);
}
