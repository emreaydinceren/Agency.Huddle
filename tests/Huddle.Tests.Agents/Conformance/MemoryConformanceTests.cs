using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// FC §6.15 (Task 12.2): whether <see cref="DotAcpAgentHostFactory.StartAsync"/> creates the
/// <c>memory/</c> folder next to a Persona's Work Dir, and whether the memory block reaches a real
/// composed system prompt, driven the same way <see cref="ToolPrefixTests"/> drives its tool list.
/// </summary>
public sealed class MemoryConformanceTests
{
    /// <summary>Starting a Persona's session creates <c>{WorkDir}/memory</c>, even with nothing written into it.</summary>
    [Fact]
    public async Task CreateAsync_CreatesMemoryFolderInsideWorkDir()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, cancellationToken: ct);

        IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        string workDir = new AgentPaths(options).WorkDir(persona.Name);
        string memoryDir = Path.Combine(workDir, "memory");

        Assert.True(Directory.Exists(memoryDir));
    }

    /// <summary>
    /// With the resolved Adapter Profile's <c>ReadsFiles</c> false, the memory block still reaches the composed system prompt and
    /// names the memory tool, but the sentence about changes arriving as File Changes does not (Instructions spec §6.1, U-10).
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReadsFilesFalse_MemoryBlockWithoutTheFileChangesSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Persona persona = new("nova", "You are Nova.", Adapter: "agency");
        Dictionary<string, string?> additionalConfig = new(StringComparer.Ordinal)
        {
            ["Team:Acp:Adapters:0:Id"] = "agency",
            ["Team:Acp:Adapters:0:Command"] = "node",
            ["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture",
            ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
        };

        await using MockAdapterFixture fixture =
            await MockAdapterFixture.StartAsync(persona, additionalConfig, cancellationToken: ct);

        string appendedPrompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("Your memory is the folder", appendedPrompt, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("mcp__team__memory", appendedPrompt, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.DoesNotContain("the change is listed at the start of your next Turn", appendedPrompt, StringComparison.Ordinal);
    }
}
