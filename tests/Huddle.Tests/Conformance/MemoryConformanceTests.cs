using System.Text.Json.Nodes;
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
        string workDir = Path.Combine(options.Value.DataDir, options.Value.Acp.WorkDir, persona.Name);
        string memoryDir = Path.Combine(workDir, "memory");

        Assert.True(Directory.Exists(memoryDir));
    }

    /// <summary>With the resolved Adapter Profile's <c>ReadsFiles</c> false, no memory block reaches the composed system prompt - FC §6.11.</summary>
    [Fact]
    public async Task CreateAsync_ReadsFilesFalse_NoMemoryBlock()
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

        string appendedPrompt = await MemoryConformanceTests.GetAppendedSystemPromptAsync(fixture, ct);

        Assert.DoesNotContain("Your memory is the folder", appendedPrompt, StringComparison.Ordinal);
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
