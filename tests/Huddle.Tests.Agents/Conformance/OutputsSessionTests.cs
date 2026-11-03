using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Task 95 (11.3.t, SB-R12): the wiring half of <c>PromptPartsTests</c> for slot 13. A real session start reads the member Team's
/// <c>outputs/</c> under the data folder, caps it with <c>Team:Outputs:MaxIndexEntries</c>, and the prompt the peer receives carries the part.
/// </summary>
public sealed class OutputsSessionTests
{
    private const string PersonaText = "---\nName: Nova\nTitle: Nova\nAlias: Nova\nTeams: [Home]\n---\nYou are Nova.";

    /// <summary>Two documents in <c>Home/outputs/</c>, one in a hidden folder and a PDF: the wire prompt lists the two Markdown documents under the Outputs heading and nothing else.</summary>
    [Fact]
    public async Task StartAsync_TeamOutputs_ReachTheWirePrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        Write(dataDir.Path, "Home/outputs/plan.md", "---\ndescription: Read when planning the quarter\n---\n# Plan\n");
        Write(dataDir.Path, "Home/outputs/sub/notes.md", "# Notes without a description\n");
        Write(dataDir.Path, "Home/outputs/_drafts/draft.md", "---\ndescription: Draft\n---\n");
        Write(dataDir.Path, "Home/outputs/slides.pdf", "%PDF");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            new Persona("nova", PersonaText), Config(dataDir.Path, null), cancellationToken: ct);
        string appended = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("## Outputs", appended, StringComparison.Ordinal);
        Assert.Contains("- Home/outputs/plan.md - Read when planning the quarter", appended, StringComparison.Ordinal);
        Assert.Contains("- Home/outputs/sub/notes.md", appended, StringComparison.Ordinal);
        Assert.DoesNotContain("draft.md", appended, StringComparison.Ordinal);
        Assert.DoesNotContain("slides.pdf", appended, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:Outputs:MaxIndexEntries</c> at 1 the second document is left out and the prompt says one more is in <c>Home/outputs</c>.</summary>
    [Fact]
    public async Task StartAsync_CapFromOptions_LimitsTheListing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        Write(dataDir.Path, "Home/outputs/a.md", "---\ndescription: Read when A\n---\n");
        Write(dataDir.Path, "Home/outputs/b.md", "---\ndescription: Read when B\n---\n");

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            new Persona("nova", PersonaText), Config(dataDir.Path, "1"), cancellationToken: ct);
        string appended = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("- Home/outputs/a.md - Read when A", appended, StringComparison.Ordinal);
        Assert.DoesNotContain("Read when B", appended, StringComparison.Ordinal);
        Assert.Contains("…and 1 more in Home/outputs.", appended, StringComparison.Ordinal);
    }

    /// <summary>A Team without documents adds no Outputs heading.</summary>
    [Fact]
    public async Task StartAsync_NoOutputs_AddsNoPart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        _ = Directory.CreateDirectory(Path.Combine(dataDir.Path, "Teams", "Home", "outputs"));

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            new Persona("nova", PersonaText), Config(dataDir.Path, null), cancellationToken: ct);
        string appended = await fixture.AppendedSystemPromptAsync(ct);

        Assert.DoesNotContain("## Outputs", appended, StringComparison.Ordinal);
    }

    /// <summary>The options the fixture starts the app with.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="cap"><c>Team:Outputs:MaxIndexEntries</c>, or null for the default.</param>
    private static Dictionary<string, string?> Config(string dataDir, string? cap)
    {
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:DataDir"] = dataDir };
        if (cap is not null)
        {
            config["Team:Outputs:MaxIndexEntries"] = cap;
        }

        return config;
    }

    /// <summary>Writes a file under <c>Teams/</c> in the data folder.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="relative">The <c>/</c>-separated path under <c>Teams</c>.</param>
    /// <param name="text">The text.</param>
    private static void Write(string dataDir, string relative, string text)
    {
        string path = Path.Combine(dataDir, "Teams", relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dataDir);
        File.WriteAllText(path, text);
    }
}
