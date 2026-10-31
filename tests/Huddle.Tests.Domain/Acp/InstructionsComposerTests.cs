namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins the Instructions part of <see cref="SystemPromptComposer"/> (Instructions spec §3.2): where it sits,
/// how its sections read, and that no files means no part at all.
/// </summary>
public sealed class InstructionsComposerTests
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    /// <summary>Composes a prompt for Nova with the given snapshot.</summary>
    /// <param name="instructions">The snapshot, or null for none.</param>
    private static string Compose(InstructionsSnapshot? instructions) =>
        SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            instructions: instructions);

    /// <summary>With no Instructions files, the prompt is the one composed with no snapshot at all, and has no Instructions part.</summary>
    [Fact]
    public void Compose_NoInstructions_IsIdenticalToComposingWithNone()
    {
        string withNone = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared);

        Assert.Equal(withNone, Compose(null));
        Assert.Equal(withNone, Compose(InstructionsSnapshot.Empty));
        Assert.DoesNotContain("## Instructions", withNone, StringComparison.Ordinal);
    }

    /// <summary>The Instructions part follows the Persona's own text and precedes the identity part.</summary>
    [Fact]
    public void Compose_WithInstructions_SitsAfterThePersonaTextAndBeforeIdentity()
    {
        FakePromptSource prompts = new();
        string identity = prompts.Render("systemPrompt.identity", new Dictionary<string, string> { ["{{personaName}}"] = "Nova" });

        string actual = Compose(new InstructionsSnapshot([new InstructionsSection("Huddle", "Be brief.")]));

        int persona = actual.IndexOf("You are Nova.", StringComparison.Ordinal);
        int instructions = actual.IndexOf("## Instructions", StringComparison.Ordinal);
        int identityAt = actual.IndexOf(identity, StringComparison.Ordinal);
        Assert.True(persona >= 0 && instructions > persona, "Instructions must follow the Persona text.");
        Assert.True(identityAt > instructions, "Instructions must precede the identity part.");
        Assert.Contains("You are Nova.\n\n## Instructions\n", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>Each level becomes a section under the heading, general to specific, joined by a blank line.</summary>
    [Fact]
    public void Compose_WithInstructions_RendersOneSectionPerLevelInOrder()
    {
        InstructionsSnapshot snapshot = new(
        [
            new InstructionsSection("Huddle", "Huddle text."),
            new InstructionsSection("Team Ops", "Ops text."),
            new InstructionsSection("Team Dev", "Dev text."),
            new InstructionsSection("You", "Own text."),
        ]);

        string actual = Compose(snapshot).Replace("\r\n", "\n", StringComparison.Ordinal);

        string expectedSections = "### Huddle\nHuddle text.\n\n### Team Ops\nOps text.\n\n### Team Dev\nDev text.\n\n### You\nOwn text.";
        Assert.Contains(
            "conflict, the more specific level wins: your own over your Team's, your Team's over Huddle's.\n" + expectedSections + "\n\n",
            actual,
            StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>A configured override of the section Prompt reaches the prompt.</summary>
    [Fact]
    public void Compose_SectionOverride_IsUsed()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.instructionsSection", "[{{source}}] {{text}}");

        string actual = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            prompts,
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            instructions: new InstructionsSnapshot([new InstructionsSection("You", "Own text.")]));

        Assert.Contains("[You] Own text.", actual, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }
}
