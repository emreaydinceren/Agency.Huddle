using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 91 (11.1.t, SB-R11): the wiring half of <c>HuddleMemoryIndexTests</c>. A real session start reads <c>Teams/memory/</c> under the data
/// folder and the <c>session/new</c> request the peer receives carries the Huddle-wide Memory part.
/// </summary>
public sealed class HuddleMemorySessionTests
{
    /// <summary>A topic in <c>Teams/memory/</c> and one in <c>Teams/memory/archive/</c>: the prompt on the wire lists the first and never the second.</summary>
    [Fact]
    public async Task StartAsync_TopicInTeamsMemory_ReachesTheWirePromptWithoutArchive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string memory = Path.Combine(dataDir.Path, "Teams", "memory");
        _ = Directory.CreateDirectory(Path.Combine(memory, "archive"));
        File.WriteAllText(Path.Combine(memory, "house-rules.md"), "# Always cite sources.\n\nBody.");
        File.WriteAllText(Path.Combine(memory, "archive", "old-rule.md"), "# Retired rule.\n\nBody.");
        Persona persona = new("nova", "You are Nova.");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona,
            new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path },
            cancellationToken: ct);
        string appended = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("## Huddle-wide Memory", appended, StringComparison.Ordinal);
        Assert.Contains("- house-rules.md - Always cite sources.", appended, StringComparison.Ordinal);
        Assert.DoesNotContain("old-rule.md", appended, StringComparison.Ordinal);
    }
}
