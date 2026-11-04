using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for hiring the drafts Build a team collects: <see cref="HireService.CheckDrafts"/> and <see cref="HireService.HireDrafts"/>.</summary>
public sealed class HireServiceDraftTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly AvatarStore avatars;
    private readonly HireService service;
    private readonly FakeSkillCopier installer = new();

    /// <summary>Builds a hire service over real stores in a temporary data folder.</summary>
    public HireServiceDraftTests()
    {
        this.personas = this.dir.CreatePersonaStore();
        TeamTypeCatalog types = new(this.dir.Options(), NullLogger<TeamTypeCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
        FakeSkillAvailability packs = new();
        this.avatars = new AvatarStore(this.dir.Options(), NullLogger<AvatarStore>.Instance);
        HireLimitHarness harness = new(this.dir.Options());
        this.service = new HireService(this.personas, types, packs, this.avatars, harness.Guard, this.installer, new FakeStartGate(), NullLogger<HireService>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.personas.Dispose();
        this.dir.Dispose();
    }

    private static DraftHire Draft(string name, RoleRef? role = null, byte[]? image = null) =>
        new(name, "Developer", "You build things.", role, Avatar.None with { Background = "#3e9fe0" }, image, image is null ? null : ".png");

    /// <summary>A written draft is an Agent of the Team with its title, text, role and avatar colour.</summary>
    [Fact]
    public void HireDrafts_WritesTheAgent_WithItsRoleAndAvatar()
    {
        DraftHireResult result = this.service.HireDrafts("Garden", [Draft("Ada", new RoleRef("web", "dev", "1.0.0"))]);

        Assert.Null(result.Error);
        Assert.Equal(["Ada"], result.Hired);
        PersonaEntry entry = Assert.Single(this.personas.Entries);
        Assert.Equal("Developer", entry.Title);
        Assert.Equal(["Garden"], entry.Teams);
        Assert.True(PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _));
        Assert.Equal(new RoleRef("web", "dev", "1.0.0"), identity.Role);
        Assert.Equal("#3e9fe0", this.avatars.Get("Ada").Background);
    }

    /// <summary>A draft's adapter, model, effort, Skills and packs are written to the Agent.</summary>
    [Fact]
    public void HireDrafts_CarriesAdapterModelEffortSkillsAndPacks()
    {
        DraftHire draft = Draft("Ada") with
        {
            Adapter = "claude-agent-acp",
            Model = "sonnet",
            Skills = ["a-skill"],
            Packs = [new VersionedRef("pack", "1.0.0")],
        };

        _ = this.service.HireDrafts("Garden", [draft]);

        PersonaEntry entry = Assert.Single(this.personas.Entries);
        Assert.True(PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _));
        Assert.Equal("claude-agent-acp", identity.Adapter);
        Assert.Equal(["a-skill"], identity.Skills);
        Assert.Equal([new VersionedRef("pack", "1.0.0")], identity.Packs);
        Assert.Equal("sonnet", this.personas.Get("Ada")?.Model);
    }

    /// <summary>Ensuring the Skills of a batch asks the copier once for the distinct names, ignoring each ref's source and
    /// path, and returns what is not ready.</summary>
    [Fact]
    public async Task EnsureSkillsAsync_ReturnsWhatTheCopierReportsNotReady()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SkillSetupItem notReady = FakeSkillCopier.NotDownloaded("a-skill");
        this.installer.Missing = [notReady];

        IReadOnlyList<SkillSetupItem> missing = await this.service.EnsureSkillsAsync([new SkillRef("a-skill", "acme/skills", "skills/a-skill"), new SkillRef("a-skill", "builtin", null)], ct);

        Assert.Equal([notReady], missing);
        Assert.Equal(["a-skill"], Assert.Single(this.installer.Calls));
    }

    /// <summary>A drafted library image is saved with the Agent's avatar.</summary>
    [Fact]
    public void HireDrafts_WithAnImage_SavesItWithTheAvatar()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

        _ = this.service.HireDrafts("Garden", [Draft("Ada", image: png)]);

        Assert.NotNull(this.avatars.Get("Ada").Image);
    }

    /// <summary>A draft with no text is refused before anything is written, because the Agent's own write would refuse it only after a new Team exists.</summary>
    [Fact]
    public void CheckDrafts_BlankText_IsRefused()
    {
        string? refusal = this.service.CheckDrafts("Garden", [Draft("Ada") with { Text = "  " }]);

        Assert.Equal("Ada has no instructions. Edit the draft and write what it should do.", refusal);
        Assert.Empty(this.personas.Entries);
    }

    /// <summary>A repeated name, or one an existing Agent has, is refused before anything is written.</summary>
    [Fact]
    public void CheckDrafts_RepeatedOrUsedName_IsRefused()
    {
        _ = this.personas.Add(new PersonaIdentity("Bea", "Other", "bea", Teams: []), "You are Bea.");

        string? repeated = this.service.CheckDrafts("Garden", [Draft("Ada"), Draft("ada")]);
        string? used = this.service.CheckDrafts("Garden", [Draft("BEA")]);
        string? fine = this.service.CheckDrafts("Garden", [Draft("Ada"), Draft("Cy")]);

        Assert.Equal("The name 'ada' is already in use.", repeated);
        Assert.Equal("The name 'BEA' is already in use.", used);
        Assert.Null(fine);
    }
}
