using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="AgentsMdReader"/> levels and order (Spec §6.2, SB-R06): Huddle-wide, then each Team in
/// the order given, then that Team's Projects sorted ordinally.
/// </summary>
public sealed class AgentsMdReaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-agentsmd-" + Guid.NewGuid().ToString("N"));

    private string TeamsRoot => Path.Combine(this.root, "Teams");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Writes <paramref name="text"/> to <paramref name="fileName"/> inside <paramref name="folder"/>, creating the folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="text">The text.</param>
    private static void Write(string folder, string fileName, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, fileName), text);
    }

    /// <summary>Builds the reader under test with default options.</summary>
    private static AgentsMdReader Reader() =>
        new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);

    /// <summary>The sections come Huddle-wide, then Team A, A's Projects ordinally, then Team B; reserved and underscore folders are ignored.</summary>
    [Fact]
    public void Read_AllLevels_OrdersHuddleThenTeamThenItsProjects()
    {
        Write(this.TeamsRoot, "AGENTS.md", "Huddle rule.");
        Write(Path.Combine(this.TeamsRoot, "B"), "AGENTS.md", "B rule.");
        Write(Path.Combine(this.TeamsRoot, "A"), "AGENTS.md", "A rule.");
        Write(Path.Combine(this.TeamsRoot, "A", "Zeta"), "AGENTS.md", "Zeta rule.");
        Write(Path.Combine(this.TeamsRoot, "A", "Alpha"), "AGENTS.md", "Alpha rule.");
        Write(Path.Combine(this.TeamsRoot, "A", "raw"), "AGENTS.md", "Raw rule.");
        Write(Path.Combine(this.TeamsRoot, "A", "_x"), "AGENTS.md", "Underscore rule.");

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A", "B"]);

        Assert.Equal(
            ["Huddle rule.", "A rule.", "Alpha rule.", "Zeta rule.", "B rule."],
            snapshot.Sections.Select(static section => section.Text).ToArray());
        Assert.Equal(
            [AgentsMdLevel.HuddleWide, AgentsMdLevel.Team, AgentsMdLevel.Project, AgentsMdLevel.Project, AgentsMdLevel.Team],
            snapshot.Sections.Select(static section => section.Level).ToArray());
        Assert.Equal(
            [null, "A", "A", "A", "B"],
            snapshot.Sections.Select(static section => section.Team).ToArray());
        Assert.Equal(
            [null, null, "Alpha", "Zeta", null],
            snapshot.Sections.Select(static section => section.Project).ToArray());
        Assert.Equal(
            [
                Path.Combine(this.TeamsRoot, "AGENTS.md"),
                Path.Combine(this.TeamsRoot, "A", "AGENTS.md"),
                Path.Combine(this.TeamsRoot, "A", "Alpha", "AGENTS.md"),
                Path.Combine(this.TeamsRoot, "A", "Zeta", "AGENTS.md"),
                Path.Combine(this.TeamsRoot, "B", "AGENTS.md"),
            ],
            snapshot.Sections.Select(static section => section.Path).ToArray());
    }

    /// <summary>A Team whose folder holds no AGENTS.md is absent from the snapshot, and so are the levels around it that do have one.</summary>
    [Fact]
    public void Read_TeamWithoutFile_IsAbsent()
    {
        Write(this.TeamsRoot, "AGENTS.md", "Huddle rule.");
        Directory.CreateDirectory(Path.Combine(this.TeamsRoot, "A"));
        Write(Path.Combine(this.TeamsRoot, "B"), "AGENTS.md", "B rule.");

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A", "B"]);

        Assert.Equal(["Huddle rule.", "B rule."], snapshot.Sections.Select(static section => section.Text).ToArray());
        Assert.DoesNotContain(snapshot.Sections, static section => section.Team == "A");
    }

    /// <summary>A whitespace-only file contributes no section, at any level.</summary>
    [Fact]
    public void Read_WhitespaceOnlyFile_IsAbsent()
    {
        Write(this.TeamsRoot, "AGENTS.md", "  \r\n\t\n");
        Write(Path.Combine(this.TeamsRoot, "A"), "AGENTS.md", "   ");
        Write(Path.Combine(this.TeamsRoot, "A", "Alpha"), "AGENTS.md", "\n\n");

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A"]);

        Assert.Empty(snapshot.Sections);
    }

    /// <summary>On a case-sensitive file system the lookup is exact (D-8): <c>agents.md</c> is not found.</summary>
    [Fact]
    public void Read_LowercaseFileName_NotFoundOnLinux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only a case-sensitive file system distinguishes the name.");
        Write(this.TeamsRoot, "agents.md", "Huddle rule.");
        Write(Path.Combine(this.TeamsRoot, "A"), "agents.md", "A rule.");

        AgentsMdSnapshot snapshot = Reader().Read(this.TeamsRoot, ["A"]);

        Assert.Empty(snapshot.Sections);
    }
}
