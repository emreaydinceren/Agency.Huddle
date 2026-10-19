using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Teammates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests that a role added from the hiring pool, one the Team's type does not list, is hired like any other: its persona and role reference are written.</summary>
public sealed class HireServicePooledRoleTests
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [ { "id": "flow", "file": "processes/flow.md", "default": true } ],
          "roles": [ { "id": "lead", "job": "lead", "manager": true, "required": true } ] }
        """;

    private const string PoolJson = """
        { "schemaVersion": 1, "teamTypes": [ { "id": "web", "name": "Web", "description": "Builds the web." } ],
          "jobs": [ { "id": "lead", "title": "Lead" }, { "id": "tutor", "title": "Tutor", "suggestedNames": [ "Tess" ] } ] }
        """;

    /// <summary>Hiring a pooled role writes a Teammate whose role reference names the type, the pooled role and the type's version, with the job's persona as its stance.</summary>
    [Fact]
    public void Hire_PooledRole_WritesTeammateWithThePoolPersona()
    {
        using TempDataDir dir = new();
        PersonaStore personas = new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        Dictionary<string, string> poolFiles = new(StringComparer.Ordinal)
        {
            ["hiringpool.json"] = PoolJson,
            ["personas/lead.md"] = "---\ntitle: 'Lead'\n---\nYou lead.",
            ["personas/tutor.md"] = "---\ntitle: 'Tutor'\nconsult_when: 'A lesson is needed'\n---\nYou teach.",
        };
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, poolFiles);
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/processes/flow.md"] = "# Flow",
        };
        TeamTypeCatalog types = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins, pool);
        FakeSkillAvailability packs = new();
        HireLimitHarness harness = new(TestOptions.Create());
        AvatarStore avatars = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        HireService service = new(personas, types, packs, avatars, harness.Guard, new FakeSkillCopier(), new FakeStartGate(), NullLogger<HireService>.Instance);
        CardState card = new("tutor", "Tess", new ModelPick("claude-agent-acp", "sonnet"), [], [], CardReadiness.Ready, [], []);

        HireResult result = service.Hire(new HireRequest("Web Team", "web", "tutor", card));

        Assert.True(result.Ok);
        Persona persona = Assert.IsType<Persona>(personas.Get("Tess"));
        Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
        Assert.Equal("Tutor", identity.Title);
        Assert.Equal("web/tutor@1.2.0", identity.Role?.ToString());
        (IReadOnlyList<PersonaFrontmatterField> fields, string body) = PersonaFrontmatter.Parse(persona.Text);
        Assert.Equal("A lesson is needed", fields.Single(static field => string.Equals(field.Key, "consult_when", StringComparison.Ordinal)).Value);
        Assert.Equal("You teach.", body.Trim());
    }
}
