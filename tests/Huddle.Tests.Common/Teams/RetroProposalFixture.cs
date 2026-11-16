using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Everything the retro proposal tests (Task 76) share: the Task harness, the Platform Team folder on disk with a
/// <c>AGENTS.md</c> and a <c>_team.md</c>, a room with Nova (the proposer) and the real store and service.
/// </summary>
internal sealed class RetroProposalFixture : IDisposable
{
    /// <summary>The text of <c>AGENTS.md</c> the fixture starts with.</summary>
    public const string ProcessText = "# Process\n\nReview before done.\nOne PR per Task.\n";

    /// <summary>The text of <c>_team.md</c> the fixture starts with.</summary>
    public const string TeamText = "---\nteamType: 'web@1.2.0'\nprocess: 'Process.md'\n---\nTeam notes: keep it small.\n";

    private static readonly string TypeManifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", extraJson: "\"retroRole\": \"retro\"", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "retro", "title": "Retro", "candidate": "candidates/retro.md", "maxPerTeam": 1 }
        ]
        """);

    private RetroProposalFixture(TaskToolHarness harness, string roomId, string otherRoomId, string rootRoomId, string otherTeamRoomId)
    {
        this.Harness = harness;
        this.RoomId = roomId;
        this.OtherRoomId = otherRoomId;
        this.RootRoomId = rootRoomId;
        this.OtherTeamRoomId = otherTeamRoomId;
        this.TeamFolder = Path.Combine(harness.Options.Value.DataDir, "platform-folder");
        _ = Directory.CreateDirectory(this.TeamFolder);
        File.WriteAllText(Path.Combine(this.TeamFolder, TeamPaths.AgentsMdFileName), ProcessText);
        File.WriteAllText(Path.Combine(this.TeamFolder, TeamPaths.TeamFileName), TeamText);
        this.Catalog = new FakeTeamCatalog
        {
            Teams =
            [
                new TeamSummary("Platform", [], ["Nova", "Kai", "Rae", "Sam"], true) { Manager = "Nova", TeamType = new VersionedRef("web", "1.2.0") },
                new TeamSummary("Other", [], ["Kai"], true),
            ],
        };
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? teamFile));
        DiskTeamFolders folders = new(this.TeamFolder) { File = teamFile, TeamFilePath = Path.Combine(this.TeamFolder, TeamPaths.TeamFileName) };
        this.Folders = folders;
        this.Proposals = new RetroProposalStore(folders);
        this.Retro = new RetroFileStore(folders);
        ITeamDirectory serviceDirectory = DirectoryGate.Wrap(harness.Directory, out DirectoryGate gate);
        this.Gate = gate;
        this.Service = new RetroProposalService(
            this.Proposals,
            this.Catalog,
            folders,
            harness.Personas,
            this.Retro,
            serviceDirectory,
            harness.Chat,
            this.Clock,
            this.ServiceLog);
    }

    /// <summary>What the service logged; a refused change to the Needs You level is a warning here.</summary>
    public RecordingLogger<RetroProposalService> ServiceLog { get; } = new();

    /// <summary>Holds the service's directory calls while a card test keeps a decision in flight.</summary>
    public DirectoryGate Gate { get; }

    /// <summary>The Task tool harness with Nova and Kai.</summary>
    public TaskToolHarness Harness { get; }

    /// <summary>Platform's Team Room, with the Human and Nova, the proposer: the only room the card shows in.</summary>
    public string RoomId { get; }

    /// <summary>A root room (no Team) holding the Human and Nova.</summary>
    public string RootRoomId { get; }

    /// <summary>The Team Room of the Other Team, holding the Human, Nova and Kai.</summary>
    public string OtherTeamRoomId { get; }

    /// <summary>The Human's room with Kai only.</summary>
    public string OtherRoomId { get; }

    /// <summary>The Platform Team folder.</summary>
    public string TeamFolder { get; }

    /// <summary>The Team catalog with Platform and Other.</summary>
    public FakeTeamCatalog Catalog { get; }

    /// <summary>The folders the stores use.</summary>
    public DiskTeamFolders Folders { get; }

    /// <summary>The clock the service reads.</summary>
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The proposal store.</summary>
    public RetroProposalStore Proposals { get; }

    /// <summary>The register store.</summary>
    public RetroFileStore Retro { get; }

    /// <summary>The service under test.</summary>
    public RetroProposalService Service { get; }

    /// <summary>The text of Platform's <c>AGENTS.md</c> now.</summary>
    public string ProcessNow => File.ReadAllText(Path.Combine(this.TeamFolder, TeamPaths.AgentsMdFileName));

    /// <summary>The text of Platform's <c>_team.md</c> now.</summary>
    public string TeamNow => File.ReadAllText(Path.Combine(this.TeamFolder, TeamPaths.TeamFileName));

    /// <summary>The caller ids by name: Nova (manager), Rae (Retro agent), Sam (member, no role), Kai (member without the Skill) and Ola (not a member).</summary>
    public Dictionary<string, string> Ids { get; } = new(StringComparer.Ordinal);

    /// <summary>Builds the propose tool for a caller.</summary>
    /// <param name="caller">The caller's name, a key of <see cref="Ids"/>.</param>
    /// <param name="prompts">The prompt source the tool reads its description from; the seeded default when omitted.</param>
    public ProposeRetroChangesTool Tool(string caller, IPromptSource? prompts = null)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = TypeManifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/retro.md"] = "---\ntitle: Retro\nconsult_when: retros\n---\nYou review.",
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(this.Harness.Options, NullLogger<TeamTypeCatalog>.Instance, builtins);
        return new ProposeRetroChangesTool(this.Catalog, this.Proposals, this.Harness.Personas, types, this.Harness.Directory, this.Ids[caller], prompts ?? new FakePromptSource());
    }

    /// <summary>Builds the fixture.</summary>
    /// <param name="ct">Cancels the seeding.</param>
    public static async Task<RetroProposalFixture> CreateAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Persona nova = harness.Personas.Get("Nova") ?? throw new InvalidOperationException("Harness not seeded.");
        _ = harness.Personas.Update("Nova", PersonaFrontmatter.WriteListField(nova.Text, "skills", ["retrospective"]), null, null, null);
        string[] skill = ["retrospective"];
        _ = harness.Personas.Add(new PersonaIdentity("Rae", "Retro", "rae", ["Platform"], Skills: skill, Role: new RoleRef("web", "retro", "1.2.0")), "You are Rae.");
        _ = harness.Personas.Add(new PersonaIdentity("Sam", "Dev", "sam", ["Platform"], Skills: skill), "You are Sam.");
        _ = harness.Personas.Add(new PersonaIdentity("Ola", "Retro", "ola", ["Design"], Skills: skill, Role: new RoleRef("web", "retro", "1.2.0")), "You are Ola.");
        Room room = await harness.Directory.CreateRoomAsync("Platform", [KnownIds.Human, harness.NovaId ?? ""], "Platform", teamRoom: true, ct: ct);
        Room other = await harness.Directory.CreateRoomAsync("Chat", [KnownIds.Human, harness.KaiId ?? ""], ct);
        Room root = await harness.Directory.CreateRoomAsync("Retro", [KnownIds.Human, harness.NovaId ?? ""], ct);
        Room otherTeam = await harness.Directory.CreateRoomAsync("Other", [KnownIds.Human, harness.NovaId ?? "", harness.KaiId ?? ""], "Other", teamRoom: true, ct: ct);
        RetroProposalFixture fixture = new(harness, room.Id, other.Id, root.Id, otherTeam.Id);
        fixture.Ids["Nova"] = harness.NovaId ?? "";
        fixture.Ids["Kai"] = harness.KaiId ?? "";
        foreach (string name in new[] { "Rae", "Sam", "Ola" })
        {
            User? user = await harness.Directory.UpsertAgentUserAsync(name, null, ct);
            fixture.Ids[name] = user?.Id ?? throw new InvalidOperationException("Directory refused a user.");
        }

        return fixture;
    }

    /// <summary>Stores a proposal set from Nova for Platform and returns it.</summary>
    /// <param name="changes">The changes; ids are set to 1, 2, 3 in order.</param>
    public RetroProposalSet Propose(params RetroChange[] changes)
    {
        RetroProposalSet set = new(Guid.NewGuid().ToString("N"), "Platform", "Nova", changes);
        Assert.True(this.Proposals.Add(set));
        return set;
    }

    /// <summary>Makes a change.</summary>
    /// <param name="id">The change id.</param>
    /// <param name="targetFile">The file to edit.</param>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement.</param>
    public static RetroChange Change(string id, string targetFile, string find, string replace) =>
        new(id, "Title " + id, targetFile, find, replace);

    /// <inheritdoc />
    public void Dispose() => this.Harness.Dispose();
}
