using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Task 95 (11.3.t, SB-R12, Spec §8.1 slot 13): <see cref="SessionPromptInputs.Build"/> reads the outputs of the Persona's Teams, in the
/// catalog's folder spelling, capped by its <c>maxOutputsEntries</c> argument (the host passes <c>Team:Outputs:MaxIndexEntries</c>).
/// </summary>
public sealed class OutputsSessionInputsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-outs-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary data folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Two documents in a Team's <c>outputs/</c> are listed under the catalog's spelling of the Team, whatever case the label was written in.</summary>
    [Fact]
    public void Build_TeamWithOutputs_ListsThemUnderTheCatalogSpelling()
    {
        string teamsRoot = this.Teams(("Business/outputs/q3.md", "Read when planning Q3"), ("Business/outputs/q4.md", "Read when planning Q4"));

        SessionPromptSnapshot snapshot = Build(teamsRoot, ["business"], 40);

        Assert.Equal(
            [("Business/outputs/q3.md", "Read when planning Q3"), ("Business/outputs/q4.md", "Read when planning Q4")],
            snapshot.Outputs.Entries.Select(static entry => (entry.RelativePath, entry.Description)));
        Assert.Equal(0, snapshot.Outputs.NotListed);
    }

    /// <summary>The cap is the outputs argument, not the Agent's own Memory cap: two documents with a cap of one list one and leave one in <c>Business/outputs</c>.</summary>
    [Fact]
    public void Build_MoreDocumentsThanCap_ListsCapAndReportsTheRest()
    {
        string teamsRoot = this.Teams(("Business/outputs/q3.md", "Read when planning Q3"), ("Business/outputs/q4.md", "Read when planning Q4"));

        SessionPromptSnapshot snapshot = Build(teamsRoot, ["Business"], 1);

        Assert.Equal(["Business/outputs/q3.md"], snapshot.Outputs.Entries.Select(static entry => entry.RelativePath));
        Assert.Equal(1, snapshot.Outputs.NotListed); // 2 documents - 1 listed.
        Assert.Equal([new OutputsOverflow("Business/outputs", 1)], snapshot.Outputs.Overflows);
    }

    /// <summary>A Persona in no Team gets no outputs, even when a Team has some.</summary>
    [Fact]
    public void Build_PersonaInNoTeam_HasNoOutputs()
    {
        string teamsRoot = this.Teams(("Business/outputs/q3.md", "Read when planning Q3"));

        SessionPromptSnapshot snapshot = Build(teamsRoot, [], 40);

        Assert.Empty(snapshot.Outputs.Entries);
        Assert.Equal(0, snapshot.Outputs.NotListed);
    }

    /// <summary>Writes each described document under the Teams root and returns it.</summary>
    /// <param name="files">The <c>/</c>-separated paths under the Teams root and the descriptions.</param>
    private string Teams(params (string Path, string Description)[] files)
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        foreach ((string path, string description) in files)
        {
            string full = Path.Combine(teamsRoot, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full) ?? teamsRoot);
            File.WriteAllText(full, $"---\ndescription: {description}\n---\n# Title\n");
        }

        _ = Directory.CreateDirectory(Path.Combine(this.root, "nova-memory"));
        return teamsRoot;
    }

    /// <summary>Builds Nova's session inputs against the catalog's one Team, Business.</summary>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="labels">The Persona's Team labels.</param>
    /// <param name="maxOutputs">The outputs cap.</param>
    private SessionPromptSnapshot Build(string teamsRoot, IReadOnlyList<string> labels, int maxOutputs)
    {
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);
        IReadOnlyList<TeamSummary> teams = [new TeamSummary("Business", [], ["nova"], true)];
        return SessionPromptInputs.Build(
            reader, warnings, teams, "nova", Path.Combine(this.root, "nova-memory"), 50, labels, teamsRoot, 50, maxHuddleMemoryEntries: 30, maxOutputsEntries: maxOutputs);
    }
}
