using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The real stores an Agent's profile is built from, wired the way the app wires them, with only the
/// gateway and the Team catalog faked. Shared by <c>AgentProfilesTests</c> and
/// <c>ProfileAvatarTests</c>.
/// </summary>
internal sealed class ProfileFixture : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <summary>Hands <see cref="AgentProfiles"/> a typed logger that records into <see cref="Logs"/>.</summary>
    private readonly LoggerFactory loggerFactory;

    /// <summary>Builds the stores over a fresh data directory, with one team type (<c>t01</c>) and one pool job (<c>tutor</c>).</summary>
    public ProfileFixture()
    {
        this.Personas = this.dir.CreatePersonaStore();
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["t01/team-type.json"] = Factories.TeamTypeJson.Manifest("t01", "Type 01", rolesJson: """[{ "id": "tutor", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true }]"""),
            ["t01/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["t01/processes/flow.md"] = "# Flow",
        };
        this.Pool = new HiringPoolCatalog(this.dir.Options(), NullLogger<HiringPoolCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "tutor", "title": "Tutor", "description": "Teaches.", "requires": [ "email:read" ] } ] }""",
            ["personas/tutor.md"] = "---\ntitle: Tutor\nconsult_when: asked\n---\nYou teach.",
        });
        this.Types = new TeamTypeCatalog(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, this.Pool);
        this.Avatars = new AvatarStore(this.dir.Options(), NullLogger<AvatarStore>.Instance);
        this.Directory = this.dir.CreateDirectory();
        this.Gateway = new FakeAgentGateway();
        this.Health = new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance);
        this.loggerFactory = new LoggerFactory([this.Logs]);
        this.Profiles = new AgentProfiles(this.Personas, this.Types, this.Pool, this.Avatars, this.Catalog, this.Directory, this.Gateway, this.Health, AgentProfileCardTestSupport.BuildAdapterCatalog(("claude", "Claude Code"), ("codex", "Codex")), this.ModelCatalog, new Logger<AgentProfiles>(this.loggerFactory));
        FileChatStore store = new(this.dir.Options(), NullLogger<FileChatStore>.Instance);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        this.Chat = new ChatService(this.Directory, store, events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance)
        {
            TeamCatalog = this.Catalog,
        };
    }

    /// <summary>The Persona store.</summary>
    public PersonaStore Personas { get; }

    /// <summary>The Teams the fake catalog reports.</summary>
    public FakeTeamCatalog Catalog { get; } = new();

    /// <summary>The team types.</summary>
    public TeamTypeCatalog Types { get; }

    /// <summary>The hiring pool.</summary>
    public HiringPoolCatalog Pool { get; }

    /// <summary>The avatar store.</summary>
    public AvatarStore Avatars { get; }

    /// <summary>The Room and user directory.</summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary>The gateway; mark an agent online with <c>SetOnline</c>.</summary>
    public FakeAgentGateway Gateway { get; }

    /// <summary>Reported health.</summary>
    public PersonaHealth Health { get; }

    /// <summary>How full each Agent's context window is, as the Members panel's context meter reads it.</summary>
    public ContextFill ContextFill { get; } = new();

    /// <summary>The model catalog the profile reads names from; never launches an adapter.</summary>
    public FakeModelCatalog ModelCatalog { get; } = new();

    /// <summary>What <see cref="Profiles"/> logged.</summary>
    public RecordingLoggerProvider Logs { get; } = new();

    /// <summary>The service under test.</summary>
    public AgentProfiles Profiles { get; }

    /// <summary>The chat service, over the same directory and catalog.</summary>
    public ChatService Chat { get; }

    /// <summary>Initialises the directory; call once before adding agents.</summary>
    /// <param name="ct">Cancels the write.</param>
    public Task InitializeAsync(CancellationToken ct) => this.Directory.InitializeAsync("You", ct);

    /// <summary>Saves an Agent, optionally in a Team and with the tutor role of team type <c>t01</c>.</summary>
    /// <param name="name">The Name.</param>
    /// <param name="title">The Title.</param>
    /// <param name="team">The Team label, or null.</param>
    /// <param name="withRole">Whether the Agent carries the <c>t01/tutor</c> role.</param>
    /// <param name="adapter">The Adapter id written to the Persona file, or null for the default.</param>
    /// <param name="model">The Model stored for the Agent, or null for the default.</param>
    /// <param name="effort">The Effort stored for the Agent, or null for the default.</param>
    public void AddAgent(string name, string title, string? team = null, bool withRole = false, string? adapter = null, string? model = null, string? effort = null) =>
        this.Personas.Add(
            new PersonaIdentity(name, title, name.ToLowerInvariant(), Teams: team is null ? [] : [team], Adapter: adapter, Skills: ["lint"], Role: withRole ? new RoleRef("t01", "tutor", "1.0.0") : null),
            "You help.",
            model,
            effort);

    /// <summary>Creates the Agent's agent user, as its first connection does.</summary>
    /// <param name="name">The Name.</param>
    /// <param name="ct">Cancels the write.</param>
    public async Task<User> AddAgentAsync(string name, CancellationToken ct) =>
        await this.Directory.UpsertAgentUserAsync(name, null, ct) ?? throw new InvalidOperationException("No agent user was created.");

    /// <summary>Reports a Team in the fake catalog.</summary>
    /// <param name="team">The Team name.</param>
    /// <param name="manager">Its manager, or null.</param>
    /// <param name="members">Its members.</param>
    public void AddTeam(string team, string? manager, params string[] members) =>
        this.Catalog.Teams = [.. this.Catalog.Teams, new TeamSummary(team, [], members, true) { Manager = manager }];

    /// <summary>A bUnit context with every service a <see cref="ProfileAvatar"/> resolves on open.</summary>
    public MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.Profiles);
        ctx.Services.AddSingleton<ITeamDirectory>(this.Directory);
        ctx.Services.AddSingleton<IAgentGateway>(this.Gateway);
        ctx.Services.AddSingleton(this.Personas);
        ctx.Services.AddSingleton(this.Health);
        ctx.Services.AddSingleton(this.ContextFill);
        ctx.Services.AddSingleton(this.Chat);
        ctx.Services.AddSingleton(this.Avatars);
        ctx.Services.AddSingleton(AgentProfileCardTestSupport.BuildAdapterCatalog(("claude", "Claude Code"), ("codex", "Codex")));
        ctx.Services.AddScoped<AgentProfileCardOpener>();
        return ctx;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Personas.Dispose();
        this.loggerFactory.Dispose();
        this.dir.Dispose();
    }
}
