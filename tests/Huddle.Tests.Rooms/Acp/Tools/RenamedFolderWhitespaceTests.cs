using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins that a whitespace-only segment in a Team path never throws out of the rename redirects (batch 3 review): the memory tool
/// answers with its folder-refused text, and <see cref="RenamedTeamPaths.Map"/> leaves the path as it was.
/// </summary>
public sealed class RenamedFolderWhitespaceTests
{
    /// <summary>A <c>folder</c> with a blank Team or Project segment is answered as not a usable folder, not thrown.</summary>
    [Theory]
    [InlineData(" /notes")]
    [InlineData("Growth/ ")]
    public async Task Memory_WhitespaceSegment_AnswersBadFolder(string folder)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamMemoryHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(new AgentPaths(harness.Options));
        services.AddSingleton(harness.Access);
        services.AddSingleton(new MemoryFolderGate());
        services.AddSingleton<IRecycleBin>(new RecordingRecycleBin());
        services.AddSingleton<IPromptSource>(new FakePromptSource());
        services.AddSingleton(harness.Options);
        services.AddSingleton(new MemoryWarnings(NullLogger<MemoryWarnings>.Instance));
        services.AddSingleton<ITeamRenames>(journal);
        await using ServiceProvider provider = services.BuildServiceProvider();
        MemoryTool tool = ActivatorUtilities.CreateInstance<MemoryTool>(provider, "Ada");

        string result = await tool.InvokeAsync(new JsonObject { ["command"] = "list", ["folder"] = folder }, ct);

        Assert.Contains("not a Memory folder you can use", result, StringComparison.Ordinal);
    }

    /// <summary>A path with a blank Team or Project segment under the Teams root is returned unchanged.</summary>
    [Theory]
    [InlineData("/data/Teams/ /notes.md")]
    [InlineData("/data/Teams/Growth/ /notes.md")]
    public void Map_WhitespaceSegment_LeavesThePathUnchanged(string path)
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        Assert.Equal(path, RenamedTeamPaths.Map("/data/Teams", path, journal));
    }
}
