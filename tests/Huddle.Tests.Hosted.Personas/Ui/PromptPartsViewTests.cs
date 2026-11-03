using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Agents;
using Agency.Huddle.App.Library;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins "What this Agent reads" (Spec section 6.5, SB-R16): <see cref="PromptPartsView"/> lists the
/// parts of an Agent's next session in order, each with its label and where it comes from.
/// </summary>
public sealed class PromptPartsViewTests
{
    /// <summary>The view renders one row per part, in the order the composer produced them, each with its label.</summary>
    [Fact]
    public async Task View_ListsOneRowPerPartInOrder()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        IReadOnlyList<PromptPart> parts =
        [
            new("Persona text", new PromptPartSource.Persona("coo"), "You are the COO."),
            new("Memory index", new PromptPartSource.Index("memory", 12), "- topic"),
            new("Tools", new PromptPartSource.Tools(3), "a, b, c"),
        ];

        var cut = ctx.Render<PromptPartsView>(p => p.Add(v => v.Parts, parts).Add(v => v.Adapter, "claude"));

        var labels = cut.FindAll(".prompt-part-label").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(["Persona text", "Memory index", "Tools"], labels);
    }

    /// <summary>Each source is shown as text: default or overridden Prompt, the file path, a topic count, a tool count.</summary>
    [Fact]
    public async Task View_ShowsEachPartsSourceText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<LibraryPathResolver>());
        string file = MakeLibraryFile(factory, "notes/a.md");
        IReadOnlyList<PromptPart> parts =
        [
            new("Rules", new PromptPartSource.Prompt("systemPrompt.rules", false), "x"),
            new("Tone", new PromptPartSource.Prompt("systemPrompt.tone", true), "y"),
            new("AGENTS.md block", new PromptPartSource.File(file), "z"),
            new("Memory index", new PromptPartSource.Index("memory", 12), "- topic"),
            new("Tools", new PromptPartSource.Tools(3), "a, b, c"),
        ];

        var cut = ctx.Render<PromptPartsView>(p => p.Add(v => v.Parts, parts).Add(v => v.Adapter, "claude"));

        var sources = cut.FindAll(".prompt-part-source").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(["Prompt · default", "Prompt · overridden", file, "12 topics", "3 tools"], sources);
    }

    /// <summary>A Prompt row links to its row on the Prompts tab of Settings, by key.</summary>
    [Fact]
    public async Task PromptRow_LinksToTheSettingsPromptsRow()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        IReadOnlyList<PromptPart> parts = [new("Rules", new PromptPartSource.Prompt("systemPrompt.rules", true), "x")];

        var cut = ctx.Render<PromptPartsView>(p => p.Add(v => v.Parts, parts).Add(v => v.Adapter, "claude"));

        Assert.Equal("/settings?tab=prompts#systemPrompt.rules", cut.Find(".prompt-part-row a").GetAttribute("href"));
    }

    /// <summary>A file row links to the Library page on the file's root and relative path (the shape ProcessTab.Edit builds).</summary>
    [Fact]
    public async Task FileRow_LinksToTheLibraryPath()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        LibraryPathResolver resolver = factory.Services.GetRequiredService<LibraryPathResolver>();
        ctx.Services.AddSingleton(resolver);
        string file = MakeLibraryFile(factory, "notes/a.md");
        string rootId = resolver.AllRoots[0].Id;
        IReadOnlyList<PromptPart> parts = [new("AGENTS.md block", new PromptPartSource.File(file), "z")];

        var cut = ctx.Render<PromptPartsView>(p => p.Add(v => v.Parts, parts).Add(v => v.Adapter, "claude"));

        // "notes/a.md" escaped as one data string: the slash becomes %2F.
        Assert.Equal($"/library?root={Uri.EscapeDataString(rootId)}&path=notes%2Fa.md", cut.Find(".prompt-part-row a").GetAttribute("href"));
    }

    /// <summary>The view states which Adapter it was composed for and that it reflects the last start.</summary>
    [Fact]
    public async Task View_StatesTheAdapterItWasShownFor()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<PromptPartsView>(p => p.Add(v => v.Parts, []).Add(v => v.Adapter, "claude"));

        Assert.Equal("Shown for claude, as last started", cut.Find(".prompt-parts-note").TextContent.Trim());
    }

    /// <summary>Creates a file under the first Library root so the resolver accepts it, and returns its absolute path.</summary>
    private static string MakeLibraryFile(TeamWebApplicationFactory factory, string relative)
    {
        LibraryPathResolver resolver = factory.Services.GetRequiredService<LibraryPathResolver>();
        string full = Path.Combine(resolver.AllRoots[0].FullPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? resolver.AllRoots[0].FullPath);
        File.WriteAllText(full, "x");
        return full;
    }
}
