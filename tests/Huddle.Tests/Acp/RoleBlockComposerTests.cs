namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>Pins how <see cref="SystemPromptComposer"/> adds the role block for a hired member (Task 29, MS-7).</summary>
public sealed class RoleBlockComposerTests
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    /// <summary>Builds a role block for Ada, a Developer in the Apollo Team, with one colleague and one skill.</summary>
    /// <returns>The block.</returns>
    private static RoleBlockInput Block() => new(
        "Ada",
        "Developer",
        "Apollo",
        [new ConsultLine("Grace", "Lead", "Ask about scope and priorities.")],
        [new SkillLine("code-review", "Use when reviewing a change.")]);

    /// <summary>A composition without a role block is byte-identical to the overload that never heard of one.</summary>
    [Fact]
    public void Compose_NullRoleBlock_IsByteIdenticalToToday()
    {
        Persona persona = new("Ada", "You are Ada.");
        FakePromptSource prompts = new();

        string without = SystemPromptComposer.Compose(persona, prompts, "mcp__team__get_help", ToolNames);
        string withNull = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, null, SessionScope.Shared, roleBlock: null);

        Assert.Equal(without, withNull);
    }

    /// <summary>A role block adds the name, role, Team, each colleague and each skill with its trigger, and the composed prompt carries it.</summary>
    [Fact]
    public void Compose_WithRoleBlock_RendersIdentityConsultAndSkills()
    {
        Persona persona = new("Ada", "You are Ada.");
        FakePromptSource prompts = new();

        string prompt = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, null, SessionScope.Shared, roleBlock: Block());

        string expectedBlock = prompts.Render(
            "systemPrompt.roleBlock",
            new Dictionary<string, string>
            {
                ["{{name}}"] = "Ada",
                ["{{role}}"] = "Developer",
                ["{{team}}"] = "Apollo",
                ["{{consult}}"] = "- Grace (Lead): Ask about scope and priorities.",
                ["{{skills}}"] = "- code-review: Use when reviewing a change.",
            });
        Assert.Contains(expectedBlock, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("- Grace (Lead): Ask about scope and priorities.", expectedBlock, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("- code-review: Use when reviewing a change.", expectedBlock, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>A configured override of the role block prompt reaches the composed prompt.</summary>
    [Fact]
    public void Compose_RoleBlockOverride_ReachesThePrompt()
    {
        Persona persona = new("Ada", "You are Ada.");
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.roleBlock", "ROLE {{name}}/{{role}}/{{team}}\n{{consult}}\n{{skills}}");

        string prompt = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, null, SessionScope.Shared, roleBlock: Block());

        Assert.Contains("ROLE Ada/Developer/Apollo\n- Grace (Lead): Ask about scope and priorities.\n- code-review: Use when reviewing a change.", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>A member with no colleagues or skills still gets a block, with an explicit none line for each list.</summary>
    [Fact]
    public void Compose_EmptyLists_RenderNoneLines()
    {
        Persona persona = new("Ada", "You are Ada.");
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.roleBlock", "[{{consult}}][{{skills}}]");

        string prompt = SystemPromptComposer.Compose(
            persona, prompts, "mcp__team__get_help", ToolNames, [], string.Empty, null, SessionScope.Shared,
            roleBlock: new RoleBlockInput("Ada", "Developer", "Apollo", [], []));

        Assert.Contains("[- (none)][- (none)]", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }
}
