namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Assertions about <see cref="PromptCatalog"/>'s shipped defaults as they reach a fully composed system
/// prompt, as distinct from <c>Agency.Huddle.Tests.Acp.PersonaRunnerTests</c>, which exercises
/// <see cref="SystemPromptComposer"/> and <see cref="PersonaRunner"/> as machinery and is largely
/// indifferent to what any prompt's text actually says.
/// </summary>
/// <remarks>
/// <para>
/// Task T1.11 moved two tests here from <c>PersonaRunnerTests</c>. Both used to pin literals that were
/// hard-coded inside <see cref="SystemPromptComposer"/> itself, back when there was no other way for
/// those names to reach the prompt. Now that every one of those literals is a prompt, and
/// <see cref="SystemPromptComposer.Compose"/> takes <c>toolNames</c> as a parameter, an assertion that
/// merely checks the composer's own argument came back out of its own output proves nothing about the
/// product's actual, shipped wording — it would pass identically for any string the test happened to
/// pass in. Rendering through a <see cref="FakePromptSource"/> configured with <em>no</em> overrides — it
/// falls back to exactly <see cref="PromptCatalog"/>'s defaults by construction — re-anchors both
/// assertions against the thing that can actually vary now: the default text a fresh install ships
/// with. That keeps <c>docs/agencyteam/rules.md</c> rule 32 (the <c>mcp__team__</c> tool prefix pin)
/// and the orientation-ordering guarantee meaningful.
/// </para>
/// </remarks>
public sealed class PromptDefaultsTests
{
    /// <summary>
    /// The seven real chat tools' names, each carrying its full <c>mcp__team__</c> prefix, in the same
    /// order <see cref="DotAcpAgentHostFactory"/> builds them in.
    /// </summary>
    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
        "mcp__team__follow_room",
        "mcp__team__unfollow_room",
    ];

    /// <summary>
    /// Rule 32, re-anchored: composing a system prompt from <see cref="PromptCatalog"/>'s own shipped
    /// defaults — no override configured — still names every one of the five real tools with its full
    /// <c>mcp__team__</c> prefix. Moved from <c>PersonaRunnerTests.SystemPromptComposer_NamesEveryToolWithMcpPrefix</c>,
    /// which pinned only that <see cref="SystemPromptComposer.Compose"/>'s own <c>toolNames</c> argument
    /// reappeared in its own output — true for any argument, and no longer a statement about the
    /// product's shipped text now that the composer takes that list as a parameter.
    /// </summary>
    [Fact]
    public void Compose_WithCatalogDefaults_NamesEveryToolWithMcpPrefix()
    {
        var persona = new Persona("nova", "You are Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakePromptSource(), "mcp__team__get_help", ToolNames);

        Assert.Contains("mcp__team__get_help", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__list_agents", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__create_room", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__invite_agent", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__post_message", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__follow_room", prompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__unfollow_room", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The canned orientation's shipped default is what makes progressive discovery work: it has to say
    /// what kind of application this is, and point at get_help, before the Persona's own text begins.
    /// Moved from <c>PersonaRunnerTests.SystemPromptComposer_OpensWithTheCannedOrientationNamingGetHelp</c>
    /// because that guarantee is a property of the catalog's default wording and ordering, not of
    /// <see cref="PersonaRunner"/> or the composer's mechanics.
    /// </summary>
    [Fact]
    public void Compose_WithCatalogDefaults_OpensWithOrientationNamingGetHelpBeforePersonaText()
    {
        var persona = new Persona("nova", "You are Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakePromptSource(), "mcp__team__get_help", ToolNames);

        var orientation = prompt.IndexOf("chat application", StringComparison.Ordinal);
        var help = prompt.IndexOf("mcp__team__get_help", StringComparison.Ordinal);
        var personaText = prompt.IndexOf("You are Nova.", StringComparison.Ordinal);

        Assert.True(orientation >= 0 && orientation < personaText);
        Assert.True(help >= 0 && help < personaText);
    }

    /// <summary>
    /// Every prompt's shipped default renders with no <c>{{...}}</c> token left over, once given a value
    /// for each placeholder its own <see cref="PromptDefinition.Placeholders"/> declares. This is a
    /// rendering-level check, one step past <c>PromptCatalogTests.All_EveryTokenInDefaultIsDeclaredAsAPlaceholder</c>:
    /// that test proves every token actually present in a Default is a declared placeholder, while this
    /// one proves the converse direction actually clears the template — that supplying a value for each
    /// declared placeholder leaves nothing unsubstituted.
    /// </summary>
    [Fact]
    public void AllDefaults_RenderCleanlyGivenTheirOwnDeclaredPlaceholders()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            var values = prompt.Placeholders.ToDictionary(
                placeholder => placeholder,
                placeholder => $"<{placeholder.Trim('{', '}')}>",
                StringComparer.Ordinal);

            var rendered = PromptRenderer.Render(prompt.Default, values);

            Assert.Empty(PromptRenderer.FindPlaceholders(rendered));
        }
    }

    /// <summary>
    /// The default system prompt — composed from <see cref="PromptCatalog"/>'s own defaults, with no
    /// override configured — validates clean against <see cref="PromptValidator.ValidateSystemPrompt"/>
    /// when checked against the five real, prefixed tool names. <c>PromptValidatorTests</c> already pins
    /// that every default validates clean per-prompt in isolation; this is the same guarantee one level
    /// up, at the fully composed prompt <see cref="SystemPromptComposer.Compose"/> actually produces.
    /// </summary>
    [Fact]
    public void DefaultSystemPrompt_ValidatesCleanAgainstTheRealToolNames()
    {
        var persona = new Persona("nova", "You are Nova.");

        var prompt = SystemPromptComposer.Compose(persona, new FakePromptSource(), "mcp__team__get_help", ToolNames);

        var issues = PromptValidator.ValidateSystemPrompt(prompt, ToolNames);

        Assert.Empty(issues);
    }
}
