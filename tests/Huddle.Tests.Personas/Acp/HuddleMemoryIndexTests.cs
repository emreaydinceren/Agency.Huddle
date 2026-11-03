using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Task 91 (11.1.t, SB-R11, Spec §6.3, §8.1 slot 11): the topics in <c>Teams/memory/</c> reach every Agent's prompt as the
/// <c>systemPrompt.huddleMemory</c> part (source <c>Index("huddleMemory", n)</c>), after the Agent's own Memory and before Team Memory.
/// </summary>
public sealed class HuddleMemoryIndexTests : IDisposable
{
    private const string Kind = "huddleMemory";

    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help", "mcp__team__memory"];

    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-hmem-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary data folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>Composes Nova's parts with the three Memory snapshots the way the host passes them.</summary>
    /// <param name="memory">Nova's own Memory.</param>
    /// <param name="teamMemory">Nova's Team Memory, or null.</param>
    /// <param name="huddleMemory">The Huddle-wide Memory, or null.</param>
    private static IReadOnlyList<PromptPart> Parts(MemorySnapshot? memory, TeamMemorySnapshot? teamMemory, MemorySnapshot? huddleMemory) =>
        SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory,
            SessionScope.Shared,
            teamMemory: teamMemory,
            huddleMemory: huddleMemory);

    /// <summary>One Memory file's snapshot entry under <paramref name="dir"/>.</summary>
    /// <param name="dir">The memory folder.</param>
    /// <param name="file">The file name.</param>
    /// <param name="summary">The summary line.</param>
    private static MemoryEntry Entry(string dir, string file, string summary) => new(summary, Path.Combine(dir, file));

    /// <summary>Reads the Huddle-wide Memory the way a session does: <c>Teams/memory/</c>, capped at <paramref name="max"/>.</summary>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="max">The cap.</param>
    /// <param name="teamLabels">The Persona's Team labels.</param>
    private SessionPromptSnapshot Inputs(string teamsRoot, int max, IReadOnlyList<string> teamLabels)
    {
        AgentsMdReader reader = new(new FakePromptSource(), new AgentsMdOptions(), NullLogger<AgentsMdReader>.Instance);
        MemoryWarnings warnings = new(NullLogger<MemoryWarnings>.Instance);
        return SessionPromptInputs.Build(
            reader, warnings, [], "nova", Path.Combine(this.root, "nova-memory"), 50, teamLabels, teamsRoot, 50, maxHuddleMemoryEntries: max, maxOutputsEntries: 40);
    }

    /// <summary>Writes <c>Teams/memory/&lt;name&gt;.md</c> (or under <paramref name="sub"/>) holding one heading line.</summary>
    /// <param name="teamsRoot">The Teams root.</param>
    /// <param name="sub">The sub folder under <c>memory/</c>, or empty.</param>
    /// <param name="name">The file name.</param>
    /// <param name="heading">The heading line, which is the summary.</param>
    private static void WriteTopic(string teamsRoot, string sub, string name, string heading)
    {
        string dir = Path.Combine(teamsRoot, TeamNames.MemoryFolder, sub);
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), "# " + heading + "\n\nBody.");
    }

    /// <summary>With personal Memory, Team Memory and Huddle-wide Memory present, the Huddle-wide part sits between them, carries its two entries, and reports the count as its source.</summary>
    [Fact]
    public void ComposeParts_AllThreeMemories_HuddleWideSitsBetweenMemoryAndTeamMemory()
    {
        string own = Path.Combine(this.root, "own");
        string huddle = Path.Combine(this.root, "Teams", "memory");
        string team = Path.Combine(this.root, "Teams", "Business", "memory");
        MemorySnapshot memory = new(own, [Entry(own, "mine.md", "My fact.")], 0);
        MemorySnapshot huddleMemory = new(huddle, [Entry(huddle, "a.md", "Alpha rule."), Entry(huddle, "b.md", "Beta rule.")], 0);
        TeamMemorySnapshot teamMemory = new([new TeamMemoryGroup("Business", team, new MemorySnapshot(team, [Entry(team, "t.md", "Team fact.")], 0), [])], 0);

        IReadOnlyList<PromptPart> parts = Parts(memory, teamMemory, huddleMemory);

        List<string> labels = [.. parts.Select(static part => part.Label)];
        int ownAt = labels.IndexOf("Memory");
        int huddleAt = labels.IndexOf("Huddle-wide Memory");
        int teamAt = labels.IndexOf("Team Memory");
        Assert.True(ownAt >= 0 && huddleAt == ownAt + 1 && teamAt == huddleAt + 1, "Order is Memory, Huddle-wide Memory, Team Memory.");
        PromptPart part = parts[huddleAt];
        Assert.Equal(new PromptPartSource.Index(Kind, 2), part.Source); // Two entries in the snapshot.
        Assert.Contains("## Huddle-wide Memory", part.Text, StringComparison.Ordinal);
        Assert.Contains("- a.md - Alpha rule.\n- b.md - Beta rule.", part.Text, StringComparison.Ordinal); // systemPrompt.memoryEntry: "- {{file}} - {{summary}}".
    }

    /// <summary>Over the cap the part ends with the existing "…and N more in {path}." line, naming the Huddle-wide folder.</summary>
    [Fact]
    public void ComposeParts_NotListed_EndsWithTheMoreLine()
    {
        string huddle = Path.Combine(this.root, "Teams", "memory");
        MemorySnapshot huddleMemory = new(huddle, [Entry(huddle, "a.md", "Alpha rule.")], NotListed: 4);

        PromptPart part = Parts(null, null, huddleMemory).Single(static part => part.Label == "Huddle-wide Memory");

        Assert.Contains($"- a.md - Alpha rule.\n…and 4 more in {huddle}.", part.Text, StringComparison.Ordinal); // systemPrompt.memoryMore.
    }

    /// <summary>A null snapshot and an empty one add no part, and the composed prompt is the same as without the argument.</summary>
    [Fact]
    public void ComposeParts_NullOrEmptyHuddleMemory_AddsNoPart()
    {
        MemorySnapshot empty = new(Path.Combine(this.root, "Teams", "memory"), [], 0);

        IReadOnlyList<PromptPart> withNull = Parts(null, null, null);
        IReadOnlyList<PromptPart> withEmpty = Parts(null, null, empty);

        Assert.DoesNotContain(withNull, static part => part.Source is PromptPartSource.Index { Kind: Kind });
        Assert.Equal(withNull.Select(static part => part.Text), withEmpty.Select(static part => part.Text));
    }

    /// <summary>The session inputs list <c>Teams/memory/*.md</c> for a Persona in no Team, skip <c>archive/</c>, and render in every Agent's prompt.</summary>
    [Fact]
    public void Build_TopicsAndArchive_ListsTopicsNeverArchive()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        WriteTopic(teamsRoot, string.Empty, "rules.md", "Always cite sources.");
        WriteTopic(teamsRoot, "archive", "x.md", "Retired rule.");

        SessionPromptSnapshot inputs = Inputs(teamsRoot, 30, []);

        Assert.Equal(["rules.md"], inputs.HuddleMemory.Entries.Select(static entry => entry.FileName));
        PromptPart part = Parts(inputs.Memory, inputs.TeamMemory, inputs.HuddleMemory).Single(static part => part.Label == "Huddle-wide Memory");
        Assert.Contains("- rules.md - Always cite sources.", part.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("x.md", part.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Retired rule.", part.Text, StringComparison.Ordinal);
    }

    /// <summary>Three topics with a cap of two list two and count one as not listed (the cap is <c>maxHuddleMemoryEntries</c>, not the Agent's own cap of 50).</summary>
    [Fact]
    public void Build_MoreTopicsThanCap_ListsCapAndCountsTheRest()
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        WriteTopic(teamsRoot, string.Empty, "a.md", "A.");
        WriteTopic(teamsRoot, string.Empty, "b.md", "B.");
        WriteTopic(teamsRoot, string.Empty, "c.md", "C.");

        SessionPromptSnapshot inputs = Inputs(teamsRoot, 2, []);

        Assert.Equal(["a.md", "b.md"], inputs.HuddleMemory.Entries.Select(static entry => entry.FileName)); // Ordinal by file name.
        Assert.Equal(1, inputs.HuddleMemory.NotListed); // 3 files - 2 listed.
        Assert.Equal(Path.Combine(teamsRoot, TeamNames.MemoryFolder), inputs.HuddleMemory.MemoryPath);
    }

    /// <summary>A missing <c>Teams/memory/</c> and an empty one both give no Huddle-wide part.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_MissingOrEmptyFolder_AddsNoPart(bool folderExists)
    {
        string teamsRoot = Path.Combine(this.root, "Teams");
        if (folderExists)
        {
            _ = Directory.CreateDirectory(Path.Combine(teamsRoot, TeamNames.MemoryFolder));
        }
        else
        {
            _ = Directory.CreateDirectory(teamsRoot);
        }

        SessionPromptSnapshot inputs = Inputs(teamsRoot, 30, []);

        Assert.Empty(inputs.HuddleMemory.Entries);
        Assert.DoesNotContain(Parts(inputs.Memory, inputs.TeamMemory, inputs.HuddleMemory), static part => part.Source is PromptPartSource.Index { Kind: Kind });
    }
}
