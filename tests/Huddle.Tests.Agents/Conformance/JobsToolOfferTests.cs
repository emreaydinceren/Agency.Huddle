using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Whether the <c>jobs</c> tool reaches a real composed system prompt, driven like <see cref="TaskToolOfferTests"/>: every
/// Persona may schedule its own Jobs, so the tool is gated only by <c>Team:Jobs:Enabled</c>, never by a Skill.
/// </summary>
public sealed class JobsToolOfferTests
{
    /// <summary>With Jobs on (the default) a Persona with no Skills is offered the tool by its prefixed name.</summary>
    [Fact]
    public async Task JobsEnabled_OffersTheJobsTool()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", "You are Nova."), cancellationToken: ct);

        string prompt = await fixture.AppendedSystemPromptAsync(ct);

        // contains-ok: the prompt lists every tool; this test's intent is only that jobs is among them.
        Assert.Contains("mcp__team__jobs", prompt, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:Jobs:Enabled</c> false the tool is not offered.</summary>
    [Fact]
    public async Task JobsDisabled_DoesNotOfferIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:Jobs:Enabled"] = "false" };
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", "You are Nova."), config, cancellationToken: ct);

        string prompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.DoesNotContain("mcp__team__jobs", prompt, StringComparison.Ordinal);
    }
}
