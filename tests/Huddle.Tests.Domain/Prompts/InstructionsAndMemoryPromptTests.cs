namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Prompts;

/// <summary>
/// Pins the Prompts that Instructions and Memory §6 adds or rewrites: each exists, takes effect at the
/// next session, declares the placeholders the spec names, and carries no tool-name prefix (rules.md).
/// </summary>
public sealed class InstructionsAndMemoryPromptTests
{
    /// <summary>The new Instructions Prompts declare exactly the placeholders §3.2 and §6.3 give them.</summary>
    /// <param name="key">The Prompt's key.</param>
    /// <param name="placeholders">The placeholders it declares and requires, comma separated.</param>
    [Theory]
    [InlineData("systemPrompt.instructions", "{{instructions}}")]
    [InlineData("systemPrompt.instructionsSection", "{{source}},{{text}}")]
    [InlineData("systemPrompt.instructionsCut", "{{max}},{{path}}")]
    [InlineData("systemPrompt.instructionsRefusal", "{{path}}")]
    public void InstructionsPrompts_DeclareTheirPlaceholders(string key, string placeholders)
    {
        ArgumentNullException.ThrowIfNull(placeholders);
        PromptDefinition prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Equal(placeholders.Split(','), prompt.Placeholders);
        Assert.Equal(placeholders.Split(','), prompt.RequiredPlaceholders);
    }

    /// <summary>The Instructions Prompts carry the spec's wording.</summary>
    [Fact]
    public void InstructionsPrompts_CarryTheSpecWording()
    {
        Assert.Equal("### {{source}}\n{{text}}", PromptCatalog.Get("systemPrompt.instructionsSection").Default);
        Assert.Equal("…cut at {{max}} characters; the rest is in {{path}}.", PromptCatalog.Get("systemPrompt.instructionsCut").Default);
        Assert.Equal(
            "Instructions files are written by the Human. Ask the Human to change {{path}}.",
            PromptCatalog.Get("systemPrompt.instructionsRefusal").Default);

        string instructions = PromptCatalog.Get("systemPrompt.instructions").Default.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.StartsWith("## Instructions\nThe Human wrote these standing instructions for you. Follow them in every Room.", instructions, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("the more specific level wins: your own over your Team's, your Team's over Huddle's.", instructions, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.EndsWith("{{instructions}}", instructions, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>The index line is <c>- {{file}} - {{summary}}</c>, and only <c>{{summary}}</c> is required, so an older override that used <c>{{path}}</c> still validates.</summary>
    [Fact]
    public void MemoryEntry_DefaultIsFileDashSummary_AndOnlySummaryIsRequired()
    {
        PromptDefinition prompt = PromptCatalog.Get("systemPrompt.memoryEntry");

        Assert.Equal("- {{file}} - {{summary}}", prompt.Default);
        Assert.Equal(["{{file}}", "{{summary}}", "{{path}}"], prompt.Placeholders);
        Assert.Equal(["{{summary}}"], prompt.RequiredPlaceholders);
        Assert.Empty(PromptValidator.Validate(prompt, prompt.Default));
        Assert.Empty(PromptValidator.Validate(prompt, "- {{summary}} ({{path}})"));
    }

    /// <summary>The Memory block carries the protocol and keeps the tool and changes placeholders optional, so an older override still validates.</summary>
    [Fact]
    public void MemoryBlock_NamesTheToolAndKeepsOnlyPathAndIndexRequired()
    {
        PromptDefinition prompt = PromptCatalog.Get("systemPrompt.memory");

        Assert.Equal(["{{memoryPath}}", "{{memoryIndex}}"], prompt.RequiredPlaceholders);
        Assert.Contains("{{memoryTool}}", prompt.Placeholders); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("{{memoryChanges}}", prompt.Placeholders); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("Save as you go.", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("Do not claim to", prompt.Default, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Empty(PromptValidator.Validate(prompt, prompt.Default));
    }

    /// <summary>The Team Memory frame needs the roles and the index; the two role paragraphs name their teams, paths and the tool.</summary>
    [Fact]
    public void TeamMemoryPrompts_FrameAndRoleParagraphs()
    {
        PromptDefinition frame = PromptCatalog.Get("systemPrompt.teamMemory");
        Assert.Equal(["{{teamMemoryRoles}}", "{{teamMemoryIndex}}"], frame.RequiredPlaceholders);
        Assert.Equal("## Team Memory\n{{teamMemoryRoles}}\n\n{{teamMemoryIndex}}", frame.Default);

        PromptDefinition manager = PromptCatalog.Get("systemPrompt.teamMemoryManager");
        Assert.Equal(["{{managedTeams}}", "{{managedPaths}}", "{{memoryTool}}", "{{proposeTool}}"], manager.Placeholders);

        PromptDefinition member = PromptCatalog.Get("systemPrompt.teamMemoryMember");
        Assert.Equal(["{{memberTeams}}", "{{memberPaths}}", "{{memoryTool}}", "{{proposeTool}}"], member.Placeholders);
    }

    /// <summary>Every refusal and tool Prompt exists, takes effect next session, has non-blank text and never names the tool prefix.</summary>
    /// <param name="key">The Prompt's key.</param>
    [Theory]
    [InlineData("tool.memory.description")]
    [InlineData("systemPrompt.memoryChanges")]
    [InlineData("systemPrompt.memoryConflict")]
    [InlineData("systemPrompt.memoryManagerOnly")]
    [InlineData("systemPrompt.memoryNoManager")]
    [InlineData("systemPrompt.memoryBadFile")]
    [InlineData("systemPrompt.memoryBadDescription")]
    [InlineData("systemPrompt.memoryTooLarge")]
    [InlineData("systemPrompt.memoryReadFirst")]
    [InlineData("systemPrompt.memoryDeleteNeedsVersion")]
    [InlineData("systemPrompt.pathAliasRefusal")]
    [InlineData("systemPrompt.memoryDeleted")]
    [InlineData("systemPrompt.memoryNotFound")]
    [InlineData("systemPrompt.memoryOldNotUnique")]
    [InlineData("systemPrompt.memoryBadFolder")]
    [InlineData("systemPrompt.memoryBadCommand")]
    [InlineData("systemPrompt.memoryWriteFailed")]
    [InlineData("systemPrompt.memoryReadFailed")]
    [InlineData("systemPrompt.teamMemoryManager")]
    [InlineData("systemPrompt.teamMemoryMember")]
    public void MemoryPrompts_ExistAndAreClean(string key)
    {
        PromptDefinition prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.False(string.IsNullOrWhiteSpace(prompt.Default));
        Assert.DoesNotContain("mcp__team__", prompt.Default, StringComparison.Ordinal);
        Assert.Empty(PromptValidator.Validate(prompt, prompt.Default));
    }

    /// <summary>The conflict, manager-only and no-manager refusals carry the spec's sentences.</summary>
    [Fact]
    public void Refusals_CarryTheSpecSentences()
    {
        Assert.Equal(
            "{{file}} changed since you read it. Its current text and version follow: merge your change into it and write again.",
            PromptCatalog.Get("systemPrompt.memoryConflict").Default);
        Assert.Equal(
            "Only {{team}}'s manager, {{manager}}, writes {{team}}'s Team Memory. Tell {{manager}}, or keep it in your own Memory.",
            PromptCatalog.Get("systemPrompt.memoryManagerOnly").Default);
        Assert.Equal(
            "{{team}} has no manager, so its Team Memory is written by the Human.",
            PromptCatalog.Get("systemPrompt.memoryNoManager").Default);
        Assert.Equal(
            "A memory file name is one name ending in .md, with no folders. MEMORY.md and Instructions.md are reserved: the index is built for you.",
            PromptCatalog.Get("systemPrompt.memoryBadFile").Default);
    }

    /// <summary>
    /// The Memory block tells the Agent there is no hand-kept index, because a model used to Claude Code's convention
    /// otherwise starts a MEMORY.md of its own.
    /// </summary>
    [Fact]
    public void Memory_SaysThereIsNoIndexFileToKeep()
    {
        string memory = PromptCatalog.Get("systemPrompt.memory").Default.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains( // contains-ok: prompt or tool-result text, not markup
            "Memory files are not documents: do not log changes to them. The index below is built from each file's " +
            "description, so there is no MEMORY.md to keep.\n",
            memory,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The Memory block forbids the built-in file tools on memory files and says why, because a live Agent that knew a
    /// file's path edited it with Edit and deleted it with a shell command, skipping the version check and the Recycle Bin.
    /// </summary>
    [Fact]
    public void Memory_ForbidsTheBuiltInFileTools_AndSaysWhy()
    {
        string memory = PromptCatalog.Get("systemPrompt.memory").Default.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains( // contains-ok: prompt or tool-result text, not markup
            "\nRead, write, change and delete memory files only with {{memoryTool}}, even when you know a file's path: never with " +
            "your built-in Read, Write or Edit tools or a shell command. The tool checks that nobody changed a file since you read " +
            "it, writes the description your index needs, and sends a deleted file to the Recycle Bin, where the Human can get it " +
            "back; the built-in tools do none of that. Memory files are not documents: do not log changes to them.",
            memory,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The document-history paragraph excludes memory files, because its "did I Write or Edit a Markdown file" check made a
    /// live Agent log a change to its own memory file.
    /// </summary>
    [Fact]
    public void DocHistory_SkipsMemoryFiles()
    {
        string docHistory = PromptCatalog.Get("systemPrompt.docHistory").Default;

        Assert.EndsWith( // contains-ok: prompt or tool-result text, not markup
            "Skip it for a memory file (anything in your own or a Team's memory folder, which you change with the memory tool " +
            "instead), for a file that is not Markdown, and for a document you only read to answer a question or summarise it. " +
            "It is how the Human sees who changed a document and when.",
            docHistory.TrimEnd(),
            StringComparison.Ordinal);
    }
}
