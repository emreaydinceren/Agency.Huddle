using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the edges of <see cref="AgentsMdReader"/> that the old Instructions reader's tests held: where a cut may fall,
/// a file exactly at the cap, an unreadable file, and Team names that repeat or are paths.
/// </summary>
public sealed class AgentsMdReaderEdgeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-agentsmdedge-" + Guid.NewGuid().ToString("N"));

    private string TeamsRoot => Path.Combine(this.root, "Teams");

    private string TeamFile => Path.Combine(this.TeamsRoot, "A", "AGENTS.md");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Writes <paramref name="text"/> to <c>AGENTS.md</c> inside <paramref name="folder"/>, creating the folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="text">The text.</param>
    private static void Write(string folder, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "AGENTS.md"), text);
    }

    /// <summary>Builds the reader under test with the given per-file cap and no total cap.</summary>
    /// <param name="logger">Receives the reader's log entries.</param>
    /// <param name="maxChars">The per-file cap.</param>
    private static AgentsMdReader Reader(RecordingLogger<AgentsMdReader> logger, int maxChars = 8000) =>
        new(new FakePromptSource(), new AgentsMdOptions { MaxCharsPerFile = maxChars, MaxTotalChars = 0 }, logger);

    /// <summary>The cut line the reader appends for the Team file with the given cap.</summary>
    /// <param name="max">The cap.</param>
    private string CutLine(int max) =>
        new FakePromptSource().Render(
            "systemPrompt.agentsMdCut",
            new Dictionary<string, string> { ["{{max}}"] = max.ToString(System.Globalization.CultureInfo.InvariantCulture), ["{{path}}"] = this.TeamFile });

    /// <summary>A cut that would fall between the two halves of an emoji keeps neither half.</summary>
    [Fact]
    public void Read_CutInsideASurrogatePair_BacksOffToThePairsStart()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), "aaaa😀bbbb");

        AgentsMdSnapshot snapshot = Reader(new(), maxChars: 5).Read(this.TeamsRoot, ["A"]);

        Assert.Equal("aaaa\n" + this.CutLine(5), Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>A cut that would fall between the CR and the LF of a line break keeps neither.</summary>
    [Fact]
    public void Read_CutInsideACrLf_BacksOffToTheLineBreaksStart()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), "aaaa\r\nbbbb");

        AgentsMdSnapshot snapshot = Reader(new(), maxChars: 5).Read(this.TeamsRoot, ["A"]);

        Assert.Equal("aaaa\n" + this.CutLine(5), Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>A cut right after a whole emoji and a whole CRLF keeps them: the back-off applies only to a split.</summary>
    [Fact]
    public void Read_CutAfterAWholePair_KeepsIt()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), "aa😀\r\nbbbb");

        AgentsMdSnapshot snapshot = Reader(new(), maxChars: 6).Read(this.TeamsRoot, ["A"]);

        Assert.Equal("aa😀\r\n\n" + this.CutLine(6), Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>A file exactly at the cap is not cut.</summary>
    [Fact]
    public void Read_FileAtTheCap_IsNotCut()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), new string('b', 10));

        AgentsMdSnapshot snapshot = Reader(new(), maxChars: 10).Read(this.TeamsRoot, ["A"]);

        Assert.Equal(new string('b', 10), Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>An unreadable file is left out with a Warning, and the other levels are still read.</summary>
    [Fact]
    public void Read_UnreadableFile_IsLeftOutWithAWarning()
    {
        Write(this.TeamsRoot, "Huddle rule.");
        Write(Path.Combine(this.TeamsRoot, "A"), "Locked rule.");
        RecordingLogger<AgentsMdReader> logger = new();

        using FileStream held = new(this.TeamFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        AgentsMdSnapshot snapshot = Reader(logger).Read(this.TeamsRoot, ["A"]);

        AgentsMdSection section = Assert.Single(snapshot.Sections);
        Assert.Equal(AgentsMdLevel.HuddleWide, section.Level);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>A Team named twice (in another case) is read once, and a Team name that is a path is ignored.</summary>
    [Fact]
    public void Read_DuplicateAndHostileTeamNames_AreSkipped()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), "A rule.");
        Write(this.root, "Outside rule.");

        AgentsMdSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, ["A", "a", "..", "..\\"]);

        AgentsMdSection section = Assert.Single(snapshot.Sections);
        Assert.Equal("A", section.Team);
    }

    /// <summary>A CRLF file's section keeps its bytes: the reader does not normalise line endings (Spec §6.1).</summary>
    [Fact]
    public void Read_CrLfFile_KeepsItsBytes()
    {
        Write(Path.Combine(this.TeamsRoot, "A"), "Rule one.\r\nRule two.\r\n");

        AgentsMdSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, ["A"]);

        Assert.Equal("Rule one.\r\nRule two.\r\n", Assert.Single(snapshot.Sections).Text);
    }
}
