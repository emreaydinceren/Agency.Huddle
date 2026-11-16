using Agency.Huddle.Acp.Abstractions;
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
/// Persona file, and its Model and Effort from their stores, shown by display name when the model
/// catalog already has them cached. Building a profile never waits on a probe: names that are not
/// cached yet fall back to the raw id or the default wording, and a background warm-up fills the cache.
/// The warm-up rides on the view, so the popover open now can redraw when it ends.
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
/// <param name="modelCatalog">The model catalog, read cache-only for Model and Effort names and warmed in the background when it has none.</param>
/// <param name="logger">Reports a failed background warm-up.</param>
/// <param name="human">The Human's live identity, so the Human's own avatar opens their profile too; <see langword="null"/> treats every name as an Agent's.</param>
/// <param name="hire">Names the models of each tier, so the tier chip follows a Model the Human picked; <see langword="null"/> leaves no map to check, so a chosen Model shows no chip.</param>
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
    IModelCatalog modelCatalog,
    ILogger<AgentProfiles> logger,
    IHumanIdentity? human = null,
    IHireContext? hire = null)
{
    /// <summary>Appended to a name the Agent has not chosen but runs on anyway.</summary>
    private const string DefaultSuffix = " (default)";

    /// <summary>What the Agent card says for a Model the Persona has not chosen.</summary>
    private const string DefaultModelText = "Agent default";

    /// <summary>What the Agent card says for an Effort the Persona has not chosen.</summary>
    private const string DefaultEffortText = "Model default";

    /// <summary>Guards <see cref="warming"/>.</summary>
    private readonly Lock gate = new();

    /// <summary>The latest background warm-up per Adapter, keyed on the requested Adapter id ("" for the default). One still running is shared by every card that opens meanwhile, so repeated opens neither queue probes nor miss the redraw; a finished one is replaced by the next warm-up.</summary>
    private readonly Dictionary<string, Task> warming = new(StringComparer.Ordinal);

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

        (ProfileRuntime runtime, Task? warm) = this.ReadRuntime(persona, warmIfMissing: true);
        AgentProfile profile = new(entry.Name, entry.Title, job?.Description ?? string.Empty, avatars.Get(entry.Name))
        {
            State = state,
            Runtime = runtime,
            Tier = this.TierOf(job?.Model.Tier ?? role?.Model?.Tier, persona.Adapter, persona.Model, hire?.TierMap ?? new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>()),
            SkillNames = identity?.Skills ?? [],
        };
        return new AgentProfileView(profile, manager, member ? ProfileMembership.Member : ProfileMembership.None, agent, team) { RuntimeWarmUp = warm };
    }

    /// <summary>
    /// Reads out again what the Agent called <paramref name="name"/> runs on, from the cache only and
    /// without starting a warm-up: the popover calls it once <see cref="AgentProfileView.RuntimeWarmUp"/>
    /// has finished, to redraw the names that warm-up filled.
    /// </summary>
    /// <param name="name">The Agent's Name or alias.</param>
    /// <returns>The three display strings, or <see langword="null"/> when no Persona resolves for <paramref name="name"/>.</returns>
    public ProfileRuntime? RereadRuntime(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (personas.ResolveByNameOrAlias(name) is not { } entry || personas.Get(entry.Name) is not { } persona)
        {
            return null;
        }

        return this.ReadRuntime(persona, warmIfMissing: false).Runtime;
    }

    /// <summary>
    /// Reads out what the Persona runs on. The Adapter is the one it actually launches on: the default
    /// when the Persona names none or names one that is not configured (the Agent card warns of the
    /// same fallback). Model and Effort use the display names the model catalog has already cached,
    /// never probing from here: a chosen one falls back to its raw id, and one the Persona did not
    /// choose reads "{name} (default)", falling back to "Agent default" / "Model default". When a
    /// name is missing from the cache and <paramref name="warmIfMissing"/> is set, a background warm-up
    /// is started (or the one already running joined) and handed back, so the caller can redraw when it ends.
    /// </summary>
    /// <param name="persona">The Persona with its stored Model and Effort joined in.</param>
    /// <param name="warmIfMissing">Whether a missing name starts a warm-up.</param>
    /// <returns>The three display strings, and the warm-up filling the missing names or <see langword="null"/> when none was needed.</returns>
    private (ProfileRuntime Runtime, Task? Warm) ReadRuntime(Persona persona, bool warmIfMissing)
    {
        RuntimeNames names = modelCatalog.PeekRuntime(persona.Adapter, persona.Model, persona.Effort);
        Task? warm = null;
        if (warmIfMissing && (names.Model is null || (persona.Effort is not null && names.Effort is null)))
        {
            warm = this.StartWarm(persona.Adapter, persona.Model, persona.Effort);
        }

        return (this.Compose(persona.Adapter, persona.Model, persona.Effort, names), warm);
    }

    /// <summary>
    /// Reads out what an Adapter, Model and Effort selection runs on, from the cache only: it never
    /// probes and never starts a warm-up. Build a team uses it for a draft that has no Persona yet.
    /// </summary>
    /// <param name="adapter">The chosen Adapter id, or null for the default.</param>
    /// <param name="model">The chosen model id, or null.</param>
    /// <param name="effort">The chosen effort id, or null.</param>
    /// <returns>The three display strings.</returns>
    public ProfileRuntime DescribeRuntime(string? adapter, string? model, string? effort) =>
        this.Compose(adapter, model, effort, modelCatalog.PeekRuntime(adapter, model, effort));

    /// <summary>
    /// Reads out what a new draft stores, from the cache only: it never probes and never starts a warm-up.
    /// Unlike <see cref="DescribeRuntime"/> it never names a default the draft did not choose, because a
    /// draft stores exactly what it carries: no Model reads "Agent default" and no Effort "Model default"
    /// even when the cache knows the default's name. A chosen id reads its cached name, else the raw id.
    /// </summary>
    /// <param name="adapter">The chosen Adapter id, or null for the default.</param>
    /// <param name="model">The chosen model id, or null.</param>
    /// <param name="effort">The chosen effort id, or null.</param>
    /// <returns>The three display strings.</returns>
    public ProfileRuntime DescribeDraftRuntime(string? adapter, string? model, string? effort)
    {
        RuntimeNames names = modelCatalog.PeekRuntime(adapter, model, effort);
        return new ProfileRuntime(
            (adapters.Find(adapter) ?? adapters.Default).DisplayName,
            model is null ? DefaultModelText : names.Model ?? model,
            effort is null ? DefaultEffortText : names.Effort ?? effort);
    }

    /// <summary>
    /// Learns the id of the model the Adapter runs on when none is chosen, so Build a team can store it for a
    /// new draft whose tier names no model. It reads the catalog, which probes the Adapter once and then answers
    /// from its cache, and then peeks the default the probe reported.
    /// </summary>
    /// <param name="adapter">The Adapter id, or null for the default.</param>
    /// <param name="ct">Cancels the probe.</param>
    /// <returns>The default model id, or <see langword="null"/> when the Adapter reported none or could not be probed.</returns>
    public async Task<string?> DefaultModelAsync(string? adapter, CancellationToken ct)
    {
        return await ModelCatalogDefaults.ReadDefaultModelAsync(modelCatalog, adapter, logger, ct);
    }

    /// <summary>
    /// The warning Build a team draws on a new draft whose Adapter reports nobody signed in, or <see langword="null"/> when it
    /// is signed in or Huddle cannot tell. It reads the catalog's record and never probes. It is a warning only: the hire works.
    /// </summary>
    /// <param name="adapter">The draft's Adapter id.</param>
    /// <returns>The sentence naming the Adapter, or <see langword="null"/> for anything but a recorded sign-out.</returns>
    public string? SignInWarningOf(string? adapter)
    {
        // A draft with no Adapter has nothing to sign in to; the default Adapter's record is not its own.
        if (adapter is null || modelCatalog.PeekSignIn(adapter) != AgentSignInStatus.SignedOut)
        {
            return null;
        }

        string name = (adapters.Find(adapter) ?? adapters.Default).DisplayName;
        return $"{name} does not seem to be signed in. The hire will work, but this Agent's first Turn will fail until you sign in.";
    }

    /// <summary>
    /// Asks whether the Adapter is signed in so <see cref="SignInWarningOf"/> has a record to read. Build a team calls it
    /// on Add (a user action; it costs nothing when the default-model read just probed) and on "Check again" with a refresh.
    /// </summary>
    /// <param name="adapter">The Adapter id.</param>
    /// <param name="refresh">True to probe even when a fresh record exists.</param>
    /// <param name="ct">Cancels the probe.</param>
    public async Task CheckSignInAsync(string? adapter, bool refresh, CancellationToken ct)
    {
        try
        {
            _ = await modelCatalog.CheckSignInAsync(adapter, refresh, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Safe to swallow: the warning is advisory, so a check that breaks leaves the card saying nothing, and the
            // hire it never blocks goes on as before.
            logger.LogWarning(ex, "Checking whether the Adapter is signed in failed.");
        }
    }

    /// <summary>
    /// The tier chip for an Agent. A Model equal to the Adapter's cached default is treated as no Model chosen:
    /// Build a team stores that default on a hire whose tier names no model, and the chip must not vanish for it.
    /// It reads the cache only and never probes.
    /// </summary>
    /// <param name="roleTier">The tier its role asks for, or <see langword="null"/> when it has no role.</param>
    /// <param name="adapter">The Agent's Adapter id, or null for the default.</param>
    /// <param name="model">The Model it runs on, or null for its Adapter's default.</param>
    /// <param name="tierMap">The models each Adapter's tiers name.</param>
    /// <returns>The tier to show, as <see cref="TierOfModel.Of"/> decides it.</returns>
    public ModelTier? TierOf(ModelTier? roleTier, string? adapter, string? model, IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> tierMap)
    {
        bool isAdapterDefault = model is not null && string.Equals(model, modelCatalog.PeekDefaultModel(adapter), StringComparison.Ordinal);
        return TierOfModel.Of(roleTier, isAdapterDefault ? null : model, tierMap);
    }

    /// <summary>Builds the three display strings from already-peeked names.</summary>
    /// <param name="adapter">The chosen Adapter id, or null for the default.</param>
    /// <param name="model">The chosen model id, or null.</param>
    /// <param name="effort">The chosen effort id, or null.</param>
    /// <param name="names">What the model catalog's cache holds for them.</param>
    /// <returns>The three display strings.</returns>
    private ProfileRuntime Compose(string? adapter, string? model, string? effort, RuntimeNames names) => new(
        (adapters.Find(adapter) ?? adapters.Default).DisplayName,
        AgentProfiles.Describe(model, names.Model, DefaultModelText),
        AgentProfiles.Describe(effort, names.Effort, DefaultEffortText));

    /// <summary>The text for one Model or Effort: the chosen id's name (or the id), else the default's name marked as such, else <paramref name="fallback"/>.</summary>
    /// <param name="chosen">The id the Persona chose, or null.</param>
    /// <param name="name">The cached display name for the chosen id, or for the default when none was chosen; null when not cached.</param>
    /// <param name="fallback">What to say for a default that is not cached.</param>
    private static string Describe(string? chosen, string? name, string fallback)
    {
        if (chosen is not null)
        {
            return name ?? chosen;
        }

        return name is null ? fallback : name + DefaultSuffix;
    }

    /// <summary>
    /// Starts, without awaiting it, the probes that would fill the names this card could not show. At
    /// most one warm-up per Adapter runs at a time: a card opened while one runs gets that one back. The
    /// catalog's own gate and cache deduplicate the rest.
    /// </summary>
    /// <param name="adapterId">The Persona's Adapter id.</param>
    /// <param name="model">The Persona's chosen model id, or null.</param>
    /// <param name="effort">The Persona's chosen effort id, or null.</param>
    /// <returns>The warm-up; it never faults.</returns>
    private Task StartWarm(string? adapterId, string? model, string? effort)
    {
        string key = adapterId ?? string.Empty;
        lock (this.gate)
        {
            if (this.warming.TryGetValue(key, out Task? running) && !running.IsCompleted)
            {
                return running;
            }

            Task warm = this.WarmAsync(adapterId, model, effort);
            this.warming[key] = warm;
            return warm;
        }
    }

    /// <summary>
    /// Fills the model catalog's cache for an Adapter. Never throws: it runs unobserved, so an escaping
    /// exception would surface as an unobserved task exception long after the card was drawn.
    /// </summary>
    /// <param name="adapterId">The Persona's Adapter id.</param>
    /// <param name="model">The Persona's chosen model id, or null.</param>
    /// <param name="effort">The Persona's chosen effort id, or null.</param>
    private async Task WarmAsync(string? adapterId, string? model, string? effort)
    {
        try
        {
            _ = await modelCatalog.GetAsync(adapterId, CancellationToken.None);
            if (model is not null && effort is not null)
            {
                _ = await modelCatalog.GetEffortLevelsAsync(adapterId, model, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Safe to swallow: this is a best-effort cache fill nobody awaits, the card has already
            // been drawn with its fallback text, and the catalog caches nothing on failure so the
            // next open simply tries again.
            logger.LogWarning(ex, "Warming the model catalog for the profile card failed.");
        }
    }
}
