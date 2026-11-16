using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins Task 35 (HR-12) for <see cref="TeamDuplicator"/>: a Team's roster is copied to a new Team with
/// new names, a new workspace and nothing from the source's Library, memory or Tasks. Members without a
/// role are skipped and reported, and an existing target name is refused.
/// </summary>
public sealed class TeamDuplicatorTests
{
    private const string Source = "Alpha Team";

    private const string Target = "Beta Team";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "folder", "label": "Project folder", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada", "Grace"] },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 3, "requires": ["email:read"], "suggestedNames": ["Dev1", "Dev2", "Dev3"] }
          ] }
        """;

    private const string StrictManifest = """
        { "schemaVersion": 1, "id": "strict", "version": "1.0.0", "displayName": "Strict", "order": 2,
          "workspace": { "kind": "folder", "label": "Project folder", "required": true },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true, "suggestedNames": ["Ada"] }
          ] }
        """;

    /// <summary>The roster is copied with fresh names: each role is hired again, the manager is recorded, and the type is kept.</summary>
    [Fact]
    public void Duplicate_Roster_CopiedWithNewNames()
    {
        using Fixture f = new();
        f.SeedSource();

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(["Grace", "Dev2"], result.Hired);
        PersonaIdentity grace = f.Identity("Grace");
        Assert.Equal([Target], grace.Teams);
        Assert.Equal(new RoleRef("web", "lead", "1.2.0"), grace.Role);
        PersonaIdentity dev = f.Identity("Dev2");
        Assert.Equal([Target], dev.Teams);
        Assert.Equal("dev", dev.Role?.RoleId);
        TeamFile? file = f.Folders.ReadTeamFile(Target);
        Assert.NotNull(file);
        Assert.Equal(new VersionedRef("web", "1.2.0"), file.TeamType);
        Assert.Equal("Grace", file.Manager);
        Assert.Null(file.Owns);
        Assert.Equal([Source], f.Identity("Ada").Teams);
    }

    /// <summary>The manager is hired first whatever order the source lists its members in, so the new Team's manager exists before the rest.</summary>
    [Fact]
    public void Duplicate_ManagerListedLast_ManagerHiredFirst()
    {
        using Fixture f = new();
        f.SeedSource();
        f.Catalog.Teams = [new TeamSummary(Source, [], ["Dev1", "Ada"], HasFolder: true)];

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal(["Grace", "Dev2"], result.Hired);
        Assert.Equal("Grace", f.Folders.ReadTeamFile(Target)?.Manager);
    }

    /// <summary>A source Agent with account connectors on is hired afresh: every new Agent is off (AgentConnectors spec T-9, section 10).</summary>
    [Fact]
    public void Duplicate_SourceWithConnectorsOn_NewAgentsAreOff()
    {
        using Fixture f = new();
        f.SeedSource();
        f.Personas.SetConnectors("Ada", true);
        Assert.True(f.Personas.Get("Ada")?.Connectors);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.NotEmpty(result.Hired);
        Assert.All(result.Hired, name => Assert.False(f.Personas.Get(name)?.Connectors));
        Assert.True(f.Personas.Get("Ada")?.Connectors);
    }

    /// <summary>Nothing from the source Team's Library folder appears in the new Team's folder.</summary>
    [Fact]
    public void Duplicate_SourceLibraryFiles_NotCopied()
    {
        using Fixture f = new();
        string sourceFolder = f.SeedSource();
        File.WriteAllText(Path.Combine(sourceFolder, "notes.md"), "source notes");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "memory"));
        File.WriteAllText(Path.Combine(sourceFolder, "memory", "lesson.md"), "a lesson");

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        string targetFolder = Path.Combine(Path.GetDirectoryName(sourceFolder) ?? string.Empty, Target);
        string[] files = [.. Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories).Select(Path.GetFileName).OfType<string>()];
        Assert.Equal([TeamPaths.TeamFileName], files);
    }

    /// <summary>The new Team gets the workspace passed in, not the source's.</summary>
    [Fact]
    public void Duplicate_Workspace_Replaced()
    {
        using Fixture f = new();
        f.SeedSource();
        string workspace = Path.Combine(f.Dir.Path, "new-ws");
        Directory.CreateDirectory(workspace);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, $"  {workspace}  ");

        Assert.True(result.Ok);
        Assert.Equal(workspace, f.Folders.ReadTeamFile(Target)?.Workspace);
        Assert.Equal(f.SourceWorkspace, f.Folders.ReadTeamFile(Source)?.Workspace);
    }

    /// <summary>A member with no role is skipped and reported, and a member whose role left the team type is skipped too.</summary>
    [Fact]
    public void Duplicate_MemberWithoutRole_SkippedAndReported()
    {
        using Fixture f = new();
        f.SeedSource();
        _ = f.Personas.Add(new PersonaIdentity("Legacy", "Helper", "legacy", [Source]), "Helps.");
        _ = f.Personas.Add(new PersonaIdentity("Ghost", "Ghost", "ghost", [Source], Role: new RoleRef("web", "ghost", "1.2.0")), "Boo.");
        f.Catalog.Teams = [new TeamSummary(Source, [], ["Ada", "Dev1", "Legacy", "Ghost"], HasFolder: true)];

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal(["Grace", "Dev2"], result.Hired);
        Assert.Equal(
            [new SkippedMember("Legacy", "It has no role."), new SkippedMember("Ghost", "Its role no longer exists.")],
            result.Skipped);
        Assert.Equal([Source], f.Identity("Legacy").Teams);
    }

    /// <summary>A card that cannot be hired is skipped with the card's own reasons, and the rest of the roster is still copied.</summary>
    [Fact]
    public void Duplicate_CardNeedsSetup_SkippedWithReasons()
    {
        using Fixture f = new(new NoAdapterContext());
        f.SeedSource();

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Empty(result.Hired);
        Assert.Equal(
            [new SkippedMember("Ada", "no logged-in adapter for tier Balanced"), new SkippedMember("Dev1", "no logged-in adapter for tier Balanced")],
            result.Skipped);
        Assert.NotNull(f.Folders.ReadTeamFile(Target));
    }

    /// <summary>A member whose role requires a connector that is not connected is still copied: a connector is information, never a reason to skip (the Developer role requires email:read and the fixture connects nothing).</summary>
    [Fact]
    public void Duplicate_RoleWithUnconnectedConnector_IsCopied()
    {
        using Fixture f = new();
        f.SeedSource();

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal(["Grace", "Dev2"], result.Hired);
        Assert.Empty(result.Skipped);
    }

    /// <summary>A target name that already exists is refused and nothing is hired.</summary>
    [Fact]
    public void Duplicate_ExistingTargetName_Refused()
    {
        using Fixture f = new();
        f.SeedSource();
        TeamFile other = new(new VersionedRef("web", "1.2.0"), null, null, null, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, f.Folders.CreateTeam(Target, other).Outcome);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.False(result.Ok);
        Assert.Equal("A Team with that name exists.", result.Error);
        Assert.Empty(result.Hired);
        Assert.Equal(2, f.Personas.ListNames().Count);
    }

    /// <summary>A name that is not allowed for a Team is refused with a plain message.</summary>
    [Fact]
    public void Duplicate_InvalidTargetName_Refused()
    {
        using Fixture f = new();
        f.SeedSource();

        DuplicateResult result = f.Duplicator.Duplicate(Source, "_hidden", null);

        Assert.False(result.Ok);
        Assert.Equal("That Team name is not allowed.", result.Error);
        Assert.Equal(2, f.Personas.ListNames().Count);
    }

    /// <summary>An unknown source Team, and a source Team with no team file, are refused.</summary>
    [Fact]
    public void Duplicate_SourceMissingOrWithoutTeamFile_Refused()
    {
        using Fixture f = new();
        f.Catalog.Teams = [new TeamSummary("Plain", [], [], HasFolder: true)];

        DuplicateResult unknown = f.Duplicator.Duplicate("Nobody", Target, null);
        DuplicateResult plain = f.Duplicator.Duplicate("Plain", Target, null);

        Assert.Equal("Unknown Team.", unknown.Error);
        Assert.Equal("That Team has no team file.", plain.Error);
        Assert.False(unknown.Ok);
        Assert.False(plain.Ok);
    }

    /// <summary>A workspace that is required but blank, or that does not exist, blocks the copy before anything is created.</summary>
    [Fact]
    public void Duplicate_WorkspaceInvalid_RefusedBeforeCreate()
    {
        using Fixture f = new();
        f.SeedSource();
        f.SeedStrict("Strict Team");

        DuplicateResult required = f.Duplicator.Duplicate("Strict Team", "Strict Copy", "  ");
        DuplicateResult missing = f.Duplicator.Duplicate(Source, Target, Path.Combine(f.Dir.Path, "no-such-folder"));

        Assert.Equal("Choose a workspace folder.", required.Error);
        Assert.Equal("That folder does not exist.", missing.Error);
        Assert.Null(f.Folders.ReadTeamFile("Strict Copy"));
        Assert.Null(f.Folders.ReadTeamFile(Target));
    }

    /// <summary>A hire context in which no adapter answered, so every card needs setup.</summary>
    private sealed class NoAdapterContext : IHireContext
    {
        /// <inheritdoc />
        public IReadOnlyList<AdapterAvailability> Answered { get; } = [];

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> TierMap { get; } =
            new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal);

        /// <inheritdoc />
        public IReadOnlyCollection<string> AvoidAdapters { get; } = [];

        /// <inheritdoc />
        public IConnectorStatus Connectors { get; } = new EmptyConnectorStatus();
    }

    /// <summary>The real stores behind the duplicator: two team types, a Persona store, the folder provisioner and a hire service.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly LibraryFileServiceFixture library;
        private readonly AvatarStore avatars;
        private readonly IHireContext hireContext;

        public Fixture(IHireContext? hireContext = null)
        {
            this.hireContext = hireContext ?? new RoleFixtures.Context();
            this.Dir = new TempDataDir();
            this.library = LibraryFileServiceFixture.Attach(this.Dir.Path);
            this.Personas = this.Dir.CreatePersonaStore();
            this.Catalog = new FakeTeamCatalog();
            Dictionary<string, string> builtins = new(StringComparer.Ordinal);
            foreach (string id in new[] { "web", "strict" })
            {
                builtins[$"{id}/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.";
                builtins[$"{id}/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.";
                builtins[$"{id}/processes/flow.md"] = "# Flow";
            }

            builtins["web/team-type.json"] = Manifest;
            builtins["strict/team-type.json"] = StrictManifest;
            this.Types = new TeamTypeCatalog(this.Dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
            this.Packs = new FakeSkillAvailability();
            this.avatars = new AvatarStore(this.Dir.Options(), NullLogger<AvatarStore>.Instance);
            HireLimitHarness harness = new(this.Dir.Options());
            this.Hire = new HireService(this.Personas, this.Types, this.Packs, this.avatars, harness.Guard, new FakeSkillCopier(), new FakeStartGate(), NullLogger<HireService>.Instance);
            this.Folders = new TeamFolderProvisioner(this.Personas, this.library.RootStore, this.library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, this.Catalog);
            this.Duplicator = new TeamDuplicator(this.Catalog, this.Types, this.Folders, this.Hire, this.Personas, this.Packs, this.hireContext, this.avatars);
            this.SourceWorkspace = Path.Combine(this.Dir.Path, "old-ws");
        }

        public TempDataDir Dir { get; }

        public AvatarStore Avatars => this.avatars;

        public PersonaStore Personas { get; }

        public FakeTeamCatalog Catalog { get; }

        public TeamTypeCatalog Types { get; }

        public FakeSkillAvailability Packs { get; }

        public HireService Hire { get; }

        public TeamFolderProvisioner Folders { get; }

        public TeamDuplicator Duplicator { get; }

        public string SourceWorkspace { get; }

        public PersonaIdentity Identity(string name)
        {
            Persona? persona = this.Personas.Get(name);
            Assert.NotNull(persona);
            Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
            return identity;
        }

        /// <summary>Creates the source Team with a manager and a developer; returns the Team's Library folder.</summary>
        public string SeedSource()
        {
            Directory.CreateDirectory(this.SourceWorkspace);
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", this.SourceWorkspace, null, string.Empty);
            TeamCreateResult created = this.Folders.CreateTeam(Source, file);
            Assert.Equal(TeamCreateOutcome.Created, created.Outcome);
            this.HireInto(Source, "lead");
            this.HireInto(Source, "dev");
            this.Catalog.Teams = [new TeamSummary(Source, [], ["Ada", "Dev1"], HasFolder: true)];
            return Path.GetDirectoryName(created.Path) ?? string.Empty;
        }

        /// <summary>Creates a Team of the strict type, whose workspace is required.</summary>
        public void SeedStrict(string team)
        {
            TeamFile file = new(new VersionedRef("strict", "1.0.0"), null, this.SourceWorkspace, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, this.Folders.CreateTeam(team, file).Outcome);
            this.Catalog.Teams = [.. this.Catalog.Teams, new TeamSummary(team, [], [], HasFolder: true)];
        }

        private void HireInto(string team, string roleId)
        {
            TeamTypeManifest? type = this.Types.Find("web");
            Assert.NotNull(type);
            RoleSpec role = type.Roles.First(r => r.Id == roleId);
            CardState card = CardResolver.Resolve(role, this.Personas.ListNames(), this.hireContext.Answered, this.hireContext.TierMap,
                this.hireContext.AvoidAdapters, this.Packs, this.hireContext.Connectors);
            HireResult hired = this.Hire.Hire(new HireRequest(team, "web", roleId, card));
            Assert.True(hired.Ok);
        }

        public void Dispose()
        {
            this.avatars.Dispose();
            this.Personas.Dispose();
            this.library.Dispose();
            this.Dir.Dispose();
        }
    }

    /// <summary>The copy gets its own image file with the same bytes under a new name, so replacing or deleting one Team's picture leaves the other's alone.</summary>
    [Fact]
    public void Duplicate_SourceAvatarImage_CopiedToADistinctFile()
    {
        using Fixture f = new();
        f.SeedSource();
        byte[] bytes = [1, 2, 3, 4];
        string sourceImage = f.Avatars.WriteImage(bytes, ".png");
        Assert.True(f.Folders.UpdateTeamFile(Source, current => current with { Avatar = new Avatar("GR", sourceImage, "#1E90FF") }));

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Avatar copy = f.Folders.ReadTeamFile(Target)?.Avatar ?? Avatar.None;
        Assert.NotNull(copy.Image);
        Assert.NotEqual(sourceImage, copy.Image);
        Assert.Equal(".png", Path.GetExtension(copy.Image));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(f.Avatars.ImageDirectory, copy.Image)));
        Assert.Equal("GR", copy.Label);
        Assert.Equal("#1E90FF", copy.Background);
        f.Avatars.DeleteImage(sourceImage);
        Assert.True(File.Exists(Path.Combine(f.Avatars.ImageDirectory, copy.Image)));
        Assert.Equal(sourceImage, f.Folders.ReadTeamFile(Source)?.Avatar.Image);
    }

    /// <summary>A source avatar whose image file is gone gives the copy no image rather than a name that points at nothing; the label survives.</summary>
    [Fact]
    public void Duplicate_SourceAvatarImageMissingOnDisk_CopyHasNoImage()
    {
        using Fixture f = new();
        f.SeedSource();
        Assert.True(f.Folders.UpdateTeamFile(Source, current => current with { Avatar = new Avatar("GR", "0123456789abcdef0123456789abcdef.png", null) }));

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal(new Avatar("GR", null, null), f.Folders.ReadTeamFile(Target)?.Avatar);
    }

    /// <summary>A source with no avatar gives the copy none, and writes no image file.</summary>
    [Fact]
    public void Duplicate_SourceWithoutAvatar_CopyHasNone()
    {
        using Fixture f = new();
        f.SeedSource();

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal(Avatar.None, f.Folders.ReadTeamFile(Target)?.Avatar);
        Assert.False(Directory.Exists(f.Avatars.ImageDirectory) && Directory.GetFiles(f.Avatars.ImageDirectory).Length > 0);
    }

    /// <summary>A copied member runs on exactly its source's stored Model and Effort, not the tier-map pick (the fixture's pick is <c>sonnet</c>).</summary>
    [Fact]
    public void Duplicate_SourceWithModelAndEffort_CopiesThem()
    {
        using Fixture f = new();
        f.SeedSource();
        Persona ada = f.Personas.Get("Ada") ?? throw new InvalidOperationException("Ada was not seeded.");
        _ = f.Personas.Update("Ada", ada.Text, "opus", "high", ada.WorkMode);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Persona? grace = f.Personas.Get("Grace");
        Assert.NotNull(grace);
        Assert.Equal("opus", grace.Model);
        Assert.Equal("high", grace.Effort);
    }

    /// <summary>A copied member keeps its source's stored Work Mode (#610).</summary>
    [Fact]
    public void Duplicate_SourceWithWorkMode_CopiesIt()
    {
        using Fixture f = new();
        f.SeedSource();
        Persona ada = f.Personas.Get("Ada") ?? throw new InvalidOperationException("Ada was not seeded.");
        _ = f.Personas.Update("Ada", ada.Text, ada.Model, ada.Effort, "plan");
        Assert.Equal("plan", f.Personas.Get("Ada")?.WorkMode);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal("plan", f.Personas.Get("Grace")?.WorkMode);
    }

    /// <summary>A source with no stored Work Mode gives a copy with none (#610).</summary>
    [Fact]
    public void Duplicate_SourceWithoutWorkMode_CopyHasNone()
    {
        using Fixture f = new();
        f.SeedSource();
        Assert.Null(f.Personas.Get("Dev1")?.WorkMode);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Null(f.Personas.Get("Dev2")?.WorkMode);
    }

    /// <summary>A source with no stored Model or Effort gives a copy with none, even though the tier-map pick carries a model.</summary>
    [Fact]
    public void Duplicate_SourceWithoutModelOrEffort_CopyHasNone()
    {
        using Fixture f = new();
        f.SeedSource();
        Persona dev = f.Personas.Get("Dev1") ?? throw new InvalidOperationException("Dev1 was not seeded.");
        _ = f.Personas.Update("Dev1", dev.Text, null, null, dev.WorkMode);
        Assert.Null(f.Personas.Get("Dev1")?.Model);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Persona? copy = f.Personas.Get("Dev2");
        Assert.NotNull(copy);
        Assert.Null(copy.Model);
        Assert.Null(copy.Effort);
    }

    /// <summary>A source whose frontmatter adapter was changed after the hire gives a copy on the same adapter.</summary>
    [Fact]
    public void Duplicate_SourceAdapterChanged_CopyKeepsIt()
    {
        using Fixture f = new();
        f.SeedSource();
        Persona ada = f.Personas.Get("Ada") ?? throw new InvalidOperationException("Ada was not seeded.");
        string edited = ada.Text.Replace("adapter: 'claude-agent-acp'", "adapter: 'other-acp'", StringComparison.Ordinal);
        Assert.NotEqual(ada.Text, edited);
        _ = f.Personas.Update("Ada", edited, ada.Model, ada.Effort, ada.WorkMode);
        Assert.Equal("other-acp", f.Personas.Get("Ada")?.Adapter);

        DuplicateResult result = f.Duplicator.Duplicate(Source, Target, null);

        Assert.True(result.Ok);
        Assert.Equal("other-acp", f.Personas.Get("Grace")?.Adapter);
    }
}
