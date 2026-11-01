namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins the Memory block as <see cref="SystemPromptComposer"/> renders it (Instructions spec §6.1): the memory tool's name is
/// filled in by code, the index lines read <c>file - description</c>, and the sentences that name File Changes appear only for an
/// Agent whose Turns carry them.
/// </summary>
public sealed class MemoryPromptComposerTests
{
    private static readonly string MemoryDir = Path.Combine(Path.GetTempPath(), "huddle-memory-composer", "Nova", "memory");

    private static readonly MemorySnapshot Memory = new(
        MemoryDir,
        [new MemoryEntry("Dentist visits and insurance. Read before booking.", Path.Combine(MemoryDir, "dentist.md"))],
        0);

    /// <summary>Composes Nova's prompt.</summary>
    /// <param name="toolNames">The offered tools.</param>
    /// <param name="scope">Which closing the session gets.</param>
    /// <param name="memoryChangesListed">Whether File Changes reach the Agent.</param>
    private static string Compose(IReadOnlyList<string> toolNames, SessionScope scope, bool memoryChangesListed) =>
        SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            toolNames[0],
            toolNames,
            [],
            string.Empty,
            Memory,
            scope,
            memoryChangesListed: memoryChangesListed);

    /// <summary>The tool's name comes from the offered list, prefix included, and the index line is the file's name, a dash and its description.</summary>
    [Fact]
    public void Memory_NamesTheToolWithItsPrefix_AndListsFileDashDescription()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.Shared, memoryChangesListed: true);

        Assert.Contains("memory files only with mcp__team__memory, even when you know a file's path: never with your built-in Read, Write or Edit tools", actual.ReplaceLineEndings("\n"), StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("\n- dentist.md - Dentist visits and insurance. Read before booking.\n", actual.ReplaceLineEndings("\n"), StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain(Path.Combine(MemoryDir, "dentist.md"), actual, StringComparison.Ordinal);
    }

    /// <summary>
    /// The protocol forbids saving a fact's absence and asks for a record of proposals and the Human's answers: a first run of the
    /// Long-running Rooms experiment saw an Agent write "never discussed" into memory, and re-pitch earlier proposals after a fresh
    /// start (LRR spec §9.3).
    /// </summary>
    [Fact]
    public void Memory_ForbidsSavingAGap_AndKeepsAProposalRecord()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.Shared, memoryChangesListed: true).ReplaceLineEndings(" ");

        Assert.Contains("Never save that something is missing, unknown or was never discussed", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("note the proposal and their answer in one topic file, and read that file before you propose anything", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>For an Adapter without the prefix the bare name is used, and a session that lists no memory tool still names it.</summary>
    /// <param name="tool">The tool name as offered, or null when not offered.</param>
    [Theory]
    [InlineData("memory")]
    [InlineData(null)]
    public void Memory_WithoutAPrefix_NamesTheBareTool(string? tool)
    {
        IReadOnlyList<string> toolNames = tool is null ? ["get_help"] : ["get_help", tool];

        string actual = Compose(toolNames, SessionScope.Shared, memoryChangesListed: true);

        Assert.Contains("memory files only with memory, even when you know a file's path", actual.ReplaceLineEndings("\n"), StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("mcp__", actual, StringComparison.Ordinal);
    }

    /// <summary>When File Changes reach the Agent the block carries the changes sentence, and the per-Room closing carries the carry line.</summary>
    [Fact]
    public void FileChangesListed_AddsTheChangesSentenceAndTheCarryLine()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.PerRoom, memoryChangesListed: true);

        Assert.Contains("the change is listed at the start of your next Turn in each Room.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("Two things do cross between your Rooms", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>When they do not, the block keeps the protocol but leaves out the sentence and the carry line, which both name File Changes.</summary>
    [Fact]
    public void FileChangesNotListed_LeavesOutTheChangesSentenceAndTheCarryLine()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.PerRoom, memoryChangesListed: false);

        Assert.Contains("Save as you go.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("Do not claim to\nremember what is not in your memory", actual.ReplaceLineEndings("\n"), StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("the change is listed at the start of your next Turn", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("Two things do cross between your Rooms", actual, StringComparison.Ordinal);
        Assert.Contains("Each Room you are in is a separate conversation", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>A personal memory override from before the tool, which names neither the tool nor the changes, still renders and keeps its own text.</summary>
    [Fact]
    public void OlderOverride_StillRenders()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.memory", "OLD {{memoryPath}} {{memoryIndex}}");

        string actual = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."), prompts, "mcp__team__get_help", ["mcp__team__get_help"], [], string.Empty, Memory, SessionScope.Shared);

        Assert.Contains($"OLD {MemoryDir} - dentist.md - Dentist visits", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }
}
