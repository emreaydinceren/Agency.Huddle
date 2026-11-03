using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the <see cref="AgentsMdReader"/> caps (Spec §8.2): a per-file cut with a cut line, then Projects dropped
/// from the end while the total is over <c>MaxTotalChars</c>, then the last remaining file cut to fit.
/// </summary>
public sealed class AgentsMdCapTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-agentsmdcap-" + Guid.NewGuid().ToString("N"));

    private string TeamsRoot => Path.Combine(this.root, "Teams");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Writes an <c>AGENTS.md</c> of <paramref name="length"/> copies of <paramref name="fill"/> into <paramref name="folder"/>.</summary>
    /// <param name="folder">The folder, created when missing.</param>
    /// <param name="fill">The repeated character.</param>
    /// <param name="length">The text length.</param>
    private static void Write(string folder, char fill, int length)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "AGENTS.md"), new string(fill, length));
    }

    /// <summary>Builds the reader under test with the plan's caps.</summary>
    private static AgentsMdReader Reader() =>
        new(new FakePromptSource(), new AgentsMdOptions { MaxCharsPerFile = 100, MaxTotalChars = 250 }, NullLogger<AgentsMdReader>.Instance);

    /// <summary>A 150-char Team file keeps 100 chars and ends with the cut line naming its path.</summary>
    [Fact]
    public void Read_TeamFileOverCap_IsCutWithCutLine()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), 'a', 150);
        string path = Path.Combine(this.TeamsRoot, "A", "AGENTS.md");
        string cutLine = new FakePromptSource().Render(
            "systemPrompt.instructionsCut",
            new Dictionary<string, string> { ["{{max}}"] = "100", ["{{path}}"] = path });

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A"]);

        AgentsMdSection section = Assert.Single(snapshot.Sections);
        Assert.Equal(new string('a', 100) + "\n" + cutLine, section.Text);
        Assert.Contains(path, section.Text, StringComparison.Ordinal);
    }

    /// <summary>Huddle-wide, Team and two Projects of 100 chars each: Projects go from the end, the last first, and each path is listed in Omitted.</summary>
    [Fact]
    public void Read_OverTotal_DropsProjectsFromTheEnd()
    {
        Write(this.TeamsRoot, 'h', 100);
        Write(Path.Combine(this.TeamsRoot, "A"), 't', 100);
        Write(Path.Combine(this.TeamsRoot, "A", "P1"), 'x', 100);
        Write(Path.Combine(this.TeamsRoot, "A", "P2"), 'y', 100);

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A"]);

        Assert.Equal(
            [AgentsMdLevel.HuddleWide, AgentsMdLevel.Team],
            snapshot.Sections.Select(static section => section.Level).ToArray());
        Assert.Equal(
            [Path.Combine(this.TeamsRoot, "A", "P2", "AGENTS.md"), Path.Combine(this.TeamsRoot, "A", "P1", "AGENTS.md")],
            snapshot.Omitted);
    }

    /// <summary>Dropping only the last Project is enough when the rest fits: the earlier Project stays and only the last is omitted.</summary>
    [Fact]
    public void Read_DroppingLastProjectFits_KeepsEarlierProject()
    {
        Write(this.TeamsRoot, 'h', 100);
        Write(Path.Combine(this.TeamsRoot, "A"), 't', 100);
        Write(Path.Combine(this.TeamsRoot, "A", "P1"), 'x', 40);
        Write(Path.Combine(this.TeamsRoot, "A", "P2"), 'y', 40);

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A"]);

        Assert.Equal(
            ["A", "A"],
            snapshot.Sections.Where(static section => section.Team is not null).Select(static section => section.Team).ToArray());
        Assert.Equal("P1", snapshot.Sections[^1].Project);
        Assert.Equal([Path.Combine(this.TeamsRoot, "A", "P2", "AGENTS.md")], snapshot.Omitted);
    }

    /// <summary>With no Project left to drop and the total still over, the last remaining file is cut to fit and nothing is omitted.</summary>
    [Fact]
    public void Read_StillOverAfterProjects_CutsLastRemainingFile()
    {
        Write(this.TeamsRoot, 'h', 100);
        Write(Path.Combine(this.TeamsRoot, "A"), 'a', 100);
        Write(Path.Combine(this.TeamsRoot, "B"), 'b', 100);

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A", "B"]);

        Assert.Equal(3, snapshot.Sections.Count);
        Assert.Equal(new string('h', 100), snapshot.Sections[0].Text);
        Assert.Equal(new string('a', 100), snapshot.Sections[1].Text);
        AgentsMdSection last = snapshot.Sections[2];
        Assert.StartsWith(new string('b', 30), last.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('b', 100), last.Text, StringComparison.Ordinal);
        Assert.Contains(last.Path, last.Text, StringComparison.Ordinal);
        Assert.Empty(snapshot.Omitted);
    }
}
