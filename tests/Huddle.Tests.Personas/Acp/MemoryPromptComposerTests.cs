namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
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

    /// <summary>The Memory block holds the reported.md paragraph, names the tool with its prefix and fills in the day count handed to the composer.</summary>
    [Fact]
    public void Memory_HoldsTheReportedParagraph_WithToolNameAndDaysFilledIn()
    {
        string actual = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ["mcp__team__get_help", "mcp__team__memory"],
            [],
            string.Empty,
            Memory,
            SessionScope.Shared,
            reportedDays: 9).ReplaceLineEndings(" ");

        Assert.Contains("Keep a record of what you have told the Human, in the file reported.md in your memory.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("read reported.md with mcp__team__memory; if it does not exist", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("such as \"no change since 2026-11-16\", and add no line for it.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("mcp__team__memory sets the date, starts the file when you have none, and drops lines older than 9 days.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("{{reportedDays}}", actual, StringComparison.Ordinal);
    }

    /// <summary>With no day count handed over, the paragraph names the default of 14 days.</summary>
    [Fact]
    public void Memory_WithoutADayCount_NamesTheDefault()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.Shared, memoryChangesListed: true).ReplaceLineEndings(" ");

        Assert.Contains("drops lines older than 14 days.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>With the Memory Tidy Helper on, the tidy sentence follows "Delete a file only when the Human asks you to." and joins onto the next sentence with one space.</summary>
    [Fact]
    public void MemoryTidyOn_AddsTheTidySentenceAfterTheDeleteSentence()
    {
        string actual = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ["mcp__team__get_help", "mcp__team__memory"],
            [],
            string.Empty,
            Memory,
            SessionScope.Shared,
            memoryTidy: true).ReplaceLineEndings(" ");

        Assert.Contains("Delete a file only when the Human asks you to. Huddle tidies your own memory folder about once a week:", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("Huddle never changes the lines of reported.md except to drop old ones. Give each file a description", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("{{memoryTidy}}", actual, StringComparison.Ordinal);
    }

    /// <summary>With the Helper off, the Memory block holds no tidy sentence and the delete sentence joins straight onto the next one.</summary>
    [Fact]
    public void MemoryTidyOff_LeavesTheTidySentenceOut()
    {
        string actual = Compose(["mcp__team__get_help", "mcp__team__memory"], SessionScope.Shared, memoryChangesListed: true).ReplaceLineEndings(" ");

        Assert.DoesNotContain("Huddle tidies", actual, StringComparison.Ordinal);
        Assert.Contains("Delete a file only when the Human asks you to. Give each file a description", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("{{memoryTidy}}", actual, StringComparison.Ordinal);
    }

    /// <summary>An override of the Memory prompt without the optional tidy placeholder validates with no error.</summary>
    [Fact]
    public void Override_WithoutMemoryTidy_ValidatesWithNoError()
    {
        PromptDefinition definition = PromptCatalog.Get("systemPrompt.memory");

        IReadOnlyList<PromptIssue> issues = PromptValidator.Validate(definition, "OLD {{memoryPath}} {{memoryIndex}}");

        Assert.DoesNotContain(issues, issue => issue.Severity == PromptIssueSeverity.Error);
        Assert.Contains("{{memoryTidy}}", definition.Placeholders);
        Assert.DoesNotContain("{{memoryTidy}}", definition.RequiredPlaceholders);
    }

    /// <summary>An override of the Memory prompt that never mentions the optional day count still validates and renders.</summary>
    [Fact]
    public void Override_WithoutReportedDays_ValidatesWithNoError()
    {
        PromptDefinition definition = PromptCatalog.Get("systemPrompt.memory");

        IReadOnlyList<PromptIssue> issues = PromptValidator.Validate(definition, "OLD {{memoryPath}} {{memoryIndex}}");

        Assert.DoesNotContain(issues, issue => issue.Severity == PromptIssueSeverity.Error);
    }
}
