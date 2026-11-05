using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>Pins <see cref="AgentsMdScopes"/>: which AGENTS.md files apply to a named Agent (Spec §6.7).</summary>
public sealed class AgentsMdScopesTests
{
    /// <summary>An Agent in no Team has only the Huddle-wide file in scope.</summary>
    [Fact]
    public void For_PersonaInNoTeam_OnlyHuddleWide()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir, ("Solo", []));
        AgentsMdScopes scopes = new(store, new FakeTeamCatalog(), dir.Options());

        IReadOnlyList<AgentsMdFile> files = scopes.For("Solo");

        Assert.Equal(["AGENTS.md"], files.Select(file => file.RelativePath).ToList());
    }

    /// <summary>A Team label is spelled as the catalog spells it, so the relative path matches the folder.</summary>
    [Fact]
    public void For_UsesTheCatalogsSpelling()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir, ("Vera", ["business"]));
        FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Business", [], ["Vera"], false)] };
        AgentsMdScopes scopes = new(store, catalog, dir.Options());

        IReadOnlyList<AgentsMdFile> files = scopes.For("Vera");

        Assert.Equal(["AGENTS.md", "Business/AGENTS.md"], files.Select(file => file.RelativePath).ToList());
    }

    /// <summary>A name that no Persona carries has only the Huddle-wide file in scope.</summary>
    [Fact]
    public void For_UnknownPersona_OnlyHuddleWide()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir, ("Vera", ["Business"]));
        AgentsMdScopes scopes = new(store, new FakeTeamCatalog(), dir.Options());

        IReadOnlyList<AgentsMdFile> files = scopes.For("Nobody");

        Assert.Equal(["AGENTS.md"], files.Select(file => file.RelativePath).ToList());
    }

    /// <summary>Writes one Persona file per entry, then opens a real store over them.</summary>
    /// <param name="dir">The data directory.</param>
    /// <param name="personas">Each Persona's name and Team labels.</param>
    private static PersonaStore CreateStore(TempDataDir dir, params (string Name, string[] Teams)[] personas)
    {
        AgentPaths paths = new(dir.Options());
        Directory.CreateDirectory(paths.DefinitionsRoot);
        foreach ((string name, string[] teams) in personas)
        {
            string labels = string.Join(", ", teams.Select(team => $"'{team}'"));
            Directory.CreateDirectory(Path.Combine(paths.DefinitionsRoot, name));
            File.WriteAllText(
                Path.Combine(paths.DefinitionsRoot, name, $"{name}.md"),
                $"---\nname: '{name}'\ntitle: '{name}'\nalias: '{name.ToLowerInvariant()}'\nteams: [{labels}]\n---\nYou help.");
        }

        return new PersonaStore(
            paths,
            new PersonaModelStore(dir.Options()),
            new PersonaEffortStore(dir.Options()),
            new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance);
    }
}
