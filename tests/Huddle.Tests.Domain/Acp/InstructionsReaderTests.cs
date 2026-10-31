namespace Agency.Huddle.Tests.Acp;

using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="InstructionsReader"/> (Instructions spec §3.1, §3.2): the three levels, in order from
/// general to specific, each read afresh from a plain file the Human wrote.
/// </summary>
public sealed class InstructionsReaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-instructions-" + Guid.NewGuid().ToString("N"));

    private string TeamsRoot => Path.Combine(this.root, "Teams");

    private string WorkDir => Path.Combine(this.root, "Agents", "Nova", "work");

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

    /// <summary>Builds the reader under test with the given cap.</summary>
    /// <param name="logger">Receives the reader's log entries.</param>
    /// <param name="maxChars">The per-file cap.</param>
    private static InstructionsReader Reader(RecordingLogger<InstructionsReader> logger, int maxChars = 8000) =>
        new(new FakePromptSource(), maxChars, logger);

    /// <summary>The sections come from general to specific: Huddle, then each Team in the order given, then the Agent itself.</summary>
    [Fact]
    public void Read_AllLevels_OrdersHuddleThenTeamsThenYou()
    {
        Write(this.TeamsRoot, "Instructions.md", "Huddle rule.");
        Write(Path.Combine(this.TeamsRoot, "Ops"), "Instructions.md", "Ops rule.");
        Write(Path.Combine(this.TeamsRoot, "Dev"), "Instructions.md", "Dev rule.");
        Write(this.WorkDir, "Instructions.md", "Own rule.");

        InstructionsSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, ["Ops", "Dev"], this.WorkDir);

        Assert.Equal(["Huddle", "Team Ops", "Team Dev", "You"], snapshot.Sections.Select(static section => section.Source).ToArray());
        Assert.Equal(["Huddle rule.", "Ops rule.", "Dev rule.", "Own rule."], snapshot.Sections.Select(static section => section.Text).ToArray());
    }

    /// <summary>A level whose file is missing, empty or only whitespace is left out; when all are, the snapshot has no sections.</summary>
    [Fact]
    public void Read_MissingEmptyAndBlankLevels_AreLeftOut()
    {
        Write(this.TeamsRoot, "Instructions.md", "   \r\n\t");
        Write(Path.Combine(this.TeamsRoot, "Dev"), "Instructions.md", string.Empty);
        Directory.CreateDirectory(this.WorkDir);

        InstructionsSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, ["Dev", "Ops"], this.WorkDir);

        Assert.Empty(snapshot.Sections);
        Assert.True(snapshot.IsEmpty);
    }

    /// <summary>The file name is matched ignoring case, so <c>instructions.md</c> counts.</summary>
    [Fact]
    public void Read_FileNameInAnotherCase_IsStillRead()
    {
        Write(this.WorkDir, "INSTRUCTIONS.MD", "Shouted rule.");

        InstructionsSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, [], this.WorkDir);

        InstructionsSection section = Assert.Single(snapshot.Sections);
        Assert.Equal("You", section.Source);
        Assert.Equal("Shouted rule.", section.Text);
    }

    /// <summary>Frontmatter is kept: the file is read as the Human wrote it.</summary>
    [Fact]
    public void Read_FileWithFrontmatter_KeepsIt()
    {
        Write(this.WorkDir, "Instructions.md", "---\ntitle: House\n---\nBe kind.");

        InstructionsSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, [], this.WorkDir);

        Assert.Equal("---\ntitle: House\n---\nBe kind.", Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>A file over the cap is cut at the cap and the cut line, naming the cap and the file, is appended.</summary>
    [Fact]
    public void Read_FileOverTheCap_IsCutWithTheCutLine()
    {
        Write(this.WorkDir, "Instructions.md", new string('a', 50));

        InstructionsSnapshot snapshot = Reader(new(), maxChars: 10).Read(this.TeamsRoot, [], this.WorkDir);

        string text = Assert.Single(snapshot.Sections).Text;
        Assert.Equal($"{new string('a', 10)}\n…cut at 10 characters; the rest is in {Path.Combine(this.WorkDir, "Instructions.md")}.", text);
    }

    /// <summary>A file exactly at the cap is not cut.</summary>
    [Fact]
    public void Read_FileAtTheCap_IsNotCut()
    {
        Write(this.WorkDir, "Instructions.md", new string('b', 10));

        InstructionsSnapshot snapshot = Reader(new(), maxChars: 10).Read(this.TeamsRoot, [], this.WorkDir);

        Assert.Equal(new string('b', 10), Assert.Single(snapshot.Sections).Text);
    }

    /// <summary>An unreadable file is an empty level and a Warning, and the other levels are still read.</summary>
    [Fact]
    public void Read_UnreadableFile_IsEmptyLevelAndWarning()
    {
        Write(this.TeamsRoot, "Instructions.md", "Huddle rule.");
        Write(this.WorkDir, "Instructions.md", "Locked rule.");
        RecordingLogger<InstructionsReader> logger = new();

        using FileStream held = new(Path.Combine(this.WorkDir, "Instructions.md"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        InstructionsSnapshot snapshot = Reader(logger).Read(this.TeamsRoot, [], this.WorkDir);

        InstructionsSection section = Assert.Single(snapshot.Sections);
        Assert.Equal("Huddle", section.Source);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>A Team named twice (in another case) is read once, and a Team name that is a path is ignored.</summary>
    [Fact]
    public void Read_DuplicateAndHostileTeamNames_AreSkipped()
    {
        Write(Path.Combine(this.TeamsRoot, "Dev"), "Instructions.md", "Dev rule.");
        Write(this.root, "Instructions.md", "Outside rule.");

        InstructionsSnapshot snapshot = Reader(new()).Read(this.TeamsRoot, ["Dev", "dev", "..", "..\\"], this.WorkDir);

        InstructionsSection section = Assert.Single(snapshot.Sections);
        Assert.Equal("Team Dev", section.Source);
    }
}
