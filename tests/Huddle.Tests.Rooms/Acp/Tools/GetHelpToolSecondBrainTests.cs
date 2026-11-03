using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// <c>get_help</c> carries the Second Brain map (Spec 6.4) and the raw files changed since filing (Spec 6.8,
/// correction C9): the tool computes the list because a Skill runs as Agent instructions and cannot read
/// <c>team.db</c>.
/// </summary>
public sealed class GetHelpToolSecondBrainTests
{
    private const string ChangedHeading = "Raw files changed since filing";

    private static readonly string[] Teams = ["Marketing", "Ops"];

    /// <summary>The section sits after <c>getHelp.tasks</c> and before the tools section, one path line per Team.</summary>
    [Fact]
    public async Task GetHelp_WithTeams_RendersMapAfterTasksAndBeforeTools()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakePromptSource prompts = new();
        GetHelpTool tool = Build(dir, prompts, Teams);

        string help = await tool.InvokeAsync(new JsonObject(), ct);

        int tasks = help.IndexOf(prompts.Render("getHelp.tasks"), StringComparison.Ordinal);
        int map = help.IndexOf("Your Second Brain folders:", StringComparison.Ordinal);
        int tools = help.IndexOf(prompts.Render("getHelp.toolsHeader"), StringComparison.Ordinal);
        Assert.True(tasks >= 0, "getHelp.tasks is missing: the fixture must offer create_task.");
        Assert.True(map > tasks, "The Second Brain section must come after getHelp.tasks.");
        Assert.True(tools > map, "The Second Brain section must come before the tools section.");
        Assert.Contains("Teams/Marketing/ — raw/, memory/, outputs/", help, StringComparison.Ordinal);
        Assert.Contains("Teams/Ops/ — raw/, memory/, outputs/", help, StringComparison.Ordinal);
        Assert.DoesNotContain("{{paths}}", help, StringComparison.Ordinal);
    }

    /// <summary>Without Team names the section is left out.</summary>
    [Fact]
    public async Task GetHelp_WithoutTeamNames_LeavesSectionOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        GetHelpTool tool = Build(dir, new FakePromptSource(), teamNames: null);

        string help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.DoesNotContain("Your Second Brain folders:", help, StringComparison.Ordinal);
        Assert.DoesNotContain(ChangedHeading, help, StringComparison.Ordinal);
    }

    /// <summary>
    /// A raw file whose size differs, one whose modified time differs, and a Project's changed raw file are
    /// listed by relative path; unchanged files, files with no row, and another Team's files are not.
    /// </summary>
    [Fact]
    public async Task GetHelp_ChangedRawFiles_ListedUnderHeadingByRelativePath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakePromptSource prompts = new();
        RawBaselineStore store = new(dir.Options());
        Seed(dir, store, "Teams/Marketing/raw/same.txt", "abc", sizeDelta: 0, mtimeShift: TimeSpan.Zero);
        Seed(dir, store, "Teams/Marketing/raw/bigger.txt", "abc", sizeDelta: 1, mtimeShift: TimeSpan.Zero);
        Seed(dir, store, "Teams/Marketing/raw/touched.txt", "abc", sizeDelta: 0, mtimeShift: TimeSpan.FromMinutes(5));
        Seed(dir, store, "Teams/Ops/Launch/raw/Tasks/OPS-1/deep.txt", "abc", sizeDelta: 2, mtimeShift: TimeSpan.Zero);
        Seed(dir, store, "Teams/Other/raw/foreign.txt", "abc", sizeDelta: 3, mtimeShift: TimeSpan.Zero);
        Write(dir, "Teams/Marketing/raw/norow.txt", "no baseline row");
        GetHelpTool tool = Build(dir, prompts, Teams, store);

        string help = await tool.InvokeAsync(new JsonObject(), ct);

        string[] lines = help.Split('\n');
        int heading = Array.IndexOf(lines, ChangedHeading);
        Assert.True(heading >= 0, "The heading must be a line of its own, exactly as written.");
        string[] listed = [.. lines.Skip(heading + 1).TakeWhile(static line => line.StartsWith("- ", StringComparison.Ordinal))];
        // bigger.txt (size 3 vs row 4), touched.txt (mtime +5 min vs row), deep.txt (size 3 vs row 5, Project raw/Tasks/OPS-1).
        Assert.Equal(
            ["- Teams/Marketing/raw/bigger.txt", "- Teams/Marketing/raw/touched.txt", "- Teams/Ops/Launch/raw/Tasks/OPS-1/deep.txt"],
            listed.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("same.txt", help, StringComparison.Ordinal);
        Assert.DoesNotContain("norow.txt", help, StringComparison.Ordinal);
        Assert.DoesNotContain("foreign.txt", help, StringComparison.Ordinal);
    }

    /// <summary>With nothing changed the heading is absent, though the map is still there.</summary>
    [Fact]
    public async Task GetHelp_NothingChanged_OmitsHeading()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RawBaselineStore store = new(dir.Options());
        Seed(dir, store, "Teams/Marketing/raw/same.txt", "abc", sizeDelta: 0, mtimeShift: TimeSpan.Zero);
        GetHelpTool tool = Build(dir, new FakePromptSource(), Teams, store);

        string help = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("Your Second Brain folders:", help, StringComparison.Ordinal);
        Assert.DoesNotContain(ChangedHeading, help, StringComparison.Ordinal);
    }

    /// <summary>The list is computed on each call, so a file edited after the tool was built shows up.</summary>
    [Fact]
    public async Task GetHelp_FileEditedAfterConstruction_ListedOnNextCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RawBaselineStore store = new(dir.Options());
        Seed(dir, store, "Teams/Marketing/raw/live.txt", "abc", sizeDelta: 0, mtimeShift: TimeSpan.Zero);
        GetHelpTool tool = Build(dir, new FakePromptSource(), Teams, store);
        string before = await tool.InvokeAsync(new JsonObject(), ct);

        Write(dir, "Teams/Marketing/raw/live.txt", "abc plus an edit");
        string after = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.DoesNotContain(ChangedHeading, before, StringComparison.Ordinal);
        Assert.Contains("- Teams/Marketing/raw/live.txt", after, StringComparison.Ordinal);
    }

    private static GetHelpTool Build(TempDataDir dir, FakePromptSource prompts, IReadOnlyList<string>? teamNames, RawBaselineStore? store = null)
    {
        IAppTool[] others = [new NamedTool("create_task")];
        return new GetHelpTool(others, prompts, "mcp__team__", teamNames, store, dir.Options().Value);
    }

    private static void Write(TempDataDir dir, string relative, string content)
    {
        string full = Path.Combine(dir.Path, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static void Seed(TempDataDir dir, RawBaselineStore store, string relative, string content, long sizeDelta, TimeSpan mtimeShift)
    {
        Write(dir, relative, content);
        string full = Path.Combine(dir.Path, relative);
        DateTimeOffset modified = new(File.GetLastWriteTimeUtc(full), TimeSpan.Zero);
        store.Record(relative, new FileInfo(full).Length + sizeDelta, modified + mtimeShift);
    }

    /// <summary>A tool that only has a name, enough to make <c>get_help</c> offer the Tasks section.</summary>
    /// <param name="name">The tool name.</param>
    private sealed class NamedTool(string name) : IAppTool
    {
        public string Name => name;

        public string Description => "A stand-in tool.";

        public JsonObject InputSchema => new();

        public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    }
}
