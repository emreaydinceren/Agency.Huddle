using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 5.1 (Spec §6.4, §6.5; Spec §14 D-11). Pins the Skill Index end to end through the real
/// <see cref="DotAcpAgentHostFactory"/>: a Persona holding a Skill must be offered <c>read_skill</c>
/// and must carry that Skill's line in its Skill Index, and a Persona holding none must see neither -
/// so every existing golden file for a Persona without Skills stays byte-identical. These tests drive
/// a real Persona through <see cref="MockAdapterFixture"/> and inspect the composed system prompt the
/// mock ACP peer actually received, the same way <see cref="ToolPrefixTests"/> does.
/// </summary>
public sealed class SkillIndexConformanceTests
{
    /// <summary>
    /// A Persona whose frontmatter assigns <c>team-building</c> is offered the prefixed
    /// <c>read_skill</c> tool and its appended system prompt lists that Skill in its Skill Index.
    /// Also proves <c>validate_teammate</c> and <c>propose_teammates</c> gating end to end (Task
    /// 9.3, Task 10.2): <c>team-building</c>'s own <c>tools:</c> list grants both (Spec §6.8, §6.9),
    /// so the real <see cref="DotAcpAgentHostFactory"/> offers them here and their full
    /// <c>mcp__team__</c>-prefixed names reach the appended prompt's <c>{{toolNames}}</c> list, the
    /// same place <c>read_skill</c>'s name does.
    /// </summary>
    [Fact]
    public async Task StartAsync_PersonaWithTeamBuilding_PromptNamesReadSkillAndTeamBuilding()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new(
            "nova",
            "---\nName: nova\nTitle: Nova\nAlias: nov\nskills: [team-building]\n---\nYou are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        string appendedPrompt = await SkillIndexConformanceTests.GetAppendedSystemPromptAsync(fixture, ct);

        Assert.Contains("mcp__team__read_skill", appendedPrompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__validate_teammate", appendedPrompt, StringComparison.Ordinal);
        Assert.Contains("mcp__team__propose_teammates", appendedPrompt, StringComparison.Ordinal);
        Assert.Contains("- team-building: ", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Persona with no <c>skills</c> field gets an appended system prompt with no Skill Index block
    /// at all and no mention of <c>read_skill</c> anywhere - Spec §14 D-11's whole point. Also proves
    /// the other half of <c>validate_teammate</c> and <c>propose_teammates</c> gating (Task 9.3, Task
    /// 10.2): with no Skill granting either, the real <see cref="DotAcpAgentHostFactory"/> never
    /// offers them, so neither name ever reaches the appended prompt's <c>{{toolNames}}</c> list.
    /// </summary>
    [Fact]
    public async Task StartAsync_PersonaWithoutSkills_PromptHasNoSkillsBlockAndNoReadSkill()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        string appendedPrompt = await SkillIndexConformanceTests.GetAppendedSystemPromptAsync(fixture, ct);

        Assert.DoesNotContain("read_skill", appendedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("validate_teammate", appendedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("propose_teammates", appendedPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("You hold these Skills", appendedPrompt, StringComparison.Ordinal);
    }

    /// <summary>Waits for <c>session/new</c> and pulls the appended system prompt text out of its <c>_meta</c> payload.</summary>
    private static async Task<string> GetAppendedSystemPromptAsync(MockAdapterFixture fixture, CancellationToken cancellationToken)
    {
        JsonObject sessionNewRequest = await fixture.Agent.WaitForAsync("session/new", TimeSpan.FromSeconds(5));
        cancellationToken.ThrowIfCancellationRequested();

        JsonObject? parameters = sessionNewRequest["params"] as JsonObject;
        JsonObject? meta = parameters?["_meta"] as JsonObject;
        JsonObject? systemPrompt = meta?["systemPrompt"] as JsonObject;
        string? appendedPrompt = (string?)systemPrompt?["append"];

        Assert.NotNull(appendedPrompt);
        return appendedPrompt;
    }
}
