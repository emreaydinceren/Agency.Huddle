using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teammates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="HireService"/>: a resolved card becomes an ordinary Teammate file plus store rows (HR-5, HR-6).</summary>
public sealed class HireServiceTests
{
    private const string TeamName = "Web Team";

    private const string LeadCandidate = """
        ---
        title: Lead
        consult_when: planning
        ---
        You lead.
        """;

    private const string DevCandidate = """
        ---
        title: Developer
        consult_when: code changes
        ---
        You build things.
        """;

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2 }
          ] }
        """;

    private const string Pack = """
        { "id": "msbuild-authoring", "version": "1.0.0", "title": "MSBuild", "appliesTo": ["dev"],
          "consultWhenAdds": ".csproj changes", "skills": [{ "name": "target-authoring", "source": "builtin" }] }
        """;

    private static HireService Create(TempDataDir dir, out PersonaStore personas, out HireLimitHarness harness, int perTeam = 8)
    {
        personas = new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = LeadCandidate,
            ["web/candidates/dev.md"] = DevCandidate,
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
        SkillPackStore packs = new(dir.Options(), NullLogger<SkillPackStore>.Instance, new Dictionary<string, string>(StringComparer.Ordinal) { ["msbuild-authoring.json"] = Pack });
        TeamOptions options = new();
        options.Acp.MaxTeammatesPerTeam = perTeam;
        harness = new HireLimitHarness(Microsoft.Extensions.Options.Options.Create(options));
        AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        return new HireService(personas, types, packs, avatars, harness.Guard, NullLogger<HireService>.Instance);
    }

    private static HireRequest Request(string name, string roleId = "dev", string? model = "sonnet")
    {
        CardState card = new(roleId, name, new ModelPick("claude-agent-acp", model),
            [new SkillRef("test-driven-development", "builtin", null), new SkillRef("target-authoring", "builtin", null)],
            [new VersionedRef("msbuild-authoring", "1.0.0")], CardReadiness.Ready, []);
        return new HireRequest(TeamName, "web", roleId, card);
    }

    private static string FileText(TempDataDir dir, string name)
    {
        return File.ReadAllText(Path.Combine(dir.Path, "Teammates", name, $"{name}.md"));
    }

    /// <summary>A hire writes an ordinary Teammate file that parses back with its role, packs, team and merged consult_when.</summary>
    [Fact]
    public void Hire_ReadyCard_WritesTeammateFileThatParsesBack()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _);

        HireResult result = service.Hire(Request("Jane"));

        Assert.True(result.Ok);
        Assert.Equal("Jane", result.PersonaName);
        Assert.Null(result.Error);
        Persona? persona = personas.Get("Jane");
        Assert.NotNull(persona);
        Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
        Assert.Equal("Developer", identity.Title);
        Assert.Equal("jane", identity.Alias);
        Assert.Equal([TeamName], identity.Teams);
        Assert.Equal("claude-agent-acp", identity.Adapter);
        Assert.Equal(["test-driven-development", "target-authoring"], identity.Skills);
        Assert.Equal("web/dev@1.2.0", identity.Role?.ToString());
        Assert.Equal(["msbuild-authoring@1.0.0"], identity.Packs?.Select(p => p.ToString()));
        (IReadOnlyList<PersonaFrontmatterField> fields, string body) = PersonaFrontmatter.Parse(persona.Text);
        Assert.Equal("code changes; .csproj changes", fields.Single(f => string.Equals(f.Key, "consult_when", StringComparison.Ordinal)).Value);
        Assert.Equal("You build things.", body.Trim());
    }

    /// <summary>The picked model is written to the model row, and a pick with no model leaves the row unset.</summary>
    [Fact]
    public void Hire_ModelPick_WritesModelRowOnlyWhenPicked()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _);

        _ = service.Hire(Request("Jane"));
        _ = service.Hire(Request("Kai", model: null));

        Assert.Equal("sonnet", personas.Get("Jane")?.Model);
        Assert.Null(personas.Get("Kai")?.Model);
        Assert.Null(personas.Get("Jane")?.Effort);
        Assert.Null(personas.Get("Jane")?.WorkMode);
    }

    /// <summary>A name already in use is refused with a plain error and nothing is overwritten.</summary>
    [Fact]
    public void Hire_NameInUse_RefusedAndFileKept()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _);
        _ = service.Hire(Request("Jane"));
        string before = FileText(dir, "Jane");

        HireResult result = service.Hire(Request("Jane", roleId: "lead"));

        Assert.False(result.Ok);
        Assert.Equal("The name 'Jane' is already in use.", result.Error);
        Assert.Null(result.PersonaName);
        Assert.Equal(before, FileText(dir, "Jane"));
        Assert.Single(personas.Entries);
    }

    /// <summary>A file that exists on disk but is not a loaded Teammate is never overwritten.</summary>
    [Fact]
    public void Hire_FileAlreadyOnDisk_NotOverwritten()
    {
        using TempDataDir dir = new();
        string folder = Path.Combine(dir.Path, "Teammates", "Jane");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Jane.md"), "not a persona");
        HireService service = Create(dir, out PersonaStore personas, out _);

        HireResult result = service.Hire(Request("Jane"));

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("not a persona", FileText(dir, "Jane"));
        Assert.Empty(personas.Entries);
    }

    /// <summary>A role already at its MaxPerTeam is refused and nothing is written.</summary>
    [Fact]
    public void Hire_RoleFull_RefusedAndNothingWritten()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _);
        _ = service.Hire(Request("Jane"));
        _ = service.Hire(Request("Kai"));

        HireResult result = service.Hire(Request("Lou"));

        Assert.False(result.Ok);
        Assert.Equal("Team 'Web Team' already has the most Developer Teammates it allows (2).", result.Error);
        Assert.Equal(2, personas.Entries.Count);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teammates", "Lou", "Lou.md")));
    }

    /// <summary>A hire the per-Team limit refuses is reported and nothing is written.</summary>
    [Fact]
    public void Hire_TeamLimitReached_RefusedAndNothingWritten()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out HireLimitHarness harness, perTeam: 3);
        harness.SetTeam(TeamName, 3);

        HireResult result = service.Hire(Request("Jane"));

        Assert.False(result.Ok);
        Assert.Equal("Team 'Web Team' is full: 3 of 3 Teammates.", result.Error);
        Assert.Empty(personas.Entries);
        Assert.False(File.Exists(Path.Combine(dir.Path, "Teammates", "Jane", "Jane.md")));
    }

    /// <summary>An unknown team type or role is refused and nothing is written.</summary>
    [Fact]
    public void Hire_UnknownTypeOrRole_Refused()
    {
        using TempDataDir dir = new();
        HireService service = Create(dir, out PersonaStore personas, out _);
        HireRequest request = Request("Jane");

        HireResult unknownType = service.Hire(request with { TypeId = "nope" });
        HireResult unknownRole = service.Hire(request with { RoleId = "nope" });

        Assert.Equal("Unknown team type 'nope'.", unknownType.Error);
        Assert.Equal("Team type 'web' has no role 'nope'.", unknownRole.Error);
        Assert.False(unknownType.Ok);
        Assert.False(unknownRole.Ok);
        Assert.Empty(personas.Entries);
    }

    /// <summary>An avatar that cannot be saved is reported but the written Teammate stays.</summary>
    [Fact]
    public void Hire_AvatarSaveFails_ReportsErrorAndKeepsTeammate()
    {
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "avatars.json"));
        HireService service = Create(dir, out PersonaStore personas, out _);

        HireResult result = service.Hire(Request("Jane"));

        Assert.True(result.Ok);
        Assert.Equal("Jane", result.PersonaName);
        Assert.Equal("Jane was hired, but the avatar could not be saved.", result.Error);
        Assert.NotNull(personas.Get("Jane"));
    }
}
