using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Review fix 1: a Request event wakes each recipient at most once. The Request tools, the real <see cref="RequestNotifier"/> and the
/// real <see cref="TaskTriggerService"/> run together, and the wakes posted per recipient are counted after every step.
/// </summary>
public sealed class RequestWakeOnceTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    /// <summary>Platform (manager Nova, Kai) receives; Design (manager Mia) files.</summary>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
            new TeamSummary("Design", [], ["Mia"], true) { Manager = "Mia" },
        ],
    };

    /// <summary>The recipients of the wakes posted since <paramref name="seen"/> posts, sorted.</summary>
    private static string[] Recipients(Harness harness, int seen) =>
        [.. harness.Posts.Skip(seen).Select(post => post.Message.Text.Split('|')[0]).Order(StringComparer.Ordinal)];

    /// <summary>Lets the coalescing window of the last change elapse and the wakes finish.</summary>
    private static async Task SettleAsync(Harness harness)
    {
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>Everything the scenario needs.</summary>
    private sealed record Scene(Harness Harness, TaskService Tasks, FileRequestTool FileTool, TriageRequestTool TriageTool, TaskActor Kai);

    /// <summary>Builds the scene: the cast, the real tools and a started trigger.</summary>
    private static async Task<Scene> CreateSceneAsync(TempDataDir dir, CancellationToken ct)
    {
        FakeTeamCatalog catalog = Catalog();
        Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog);
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        _ = harness.Personas.Add(new PersonaIdentity("Alfred", "Chief of Staff", "alf", [], Builtin: BuiltinAgent.ChiefOfStaffMarker), "You are Alfred.");
        User mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct) ?? throw new InvalidOperationException("No Mia.");
        User alfred = await harness.Directory.UpsertAgentUserAsync("Alfred", null, ct) ?? throw new InvalidOperationException("No Alfred.");
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        _ = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id, alfred.Id], ct);

        TaskService tasks = new(harness.Store, new TaskIdAllocator(dir.Options()), harness.Events, harness.Personas, new EmptySprintCatalog(), dir.Options(), harness.Clock, NullLogger<TaskService>.Instance);
        RequestNotifier notifier = new(harness.Trigger, catalog, harness.Personas, NullLogger<RequestNotifier>.Instance);
        FileRequestTool fileTool = new(catalog, tasks, harness.Store, notifier, harness.Directory, mia.Id, harness.Prompts);
        TriageRequestTool triageTool = new(catalog, tasks, harness.Store, notifier, harness.Directory, cast.Nova.Id, harness.Prompts);
        return new Scene(harness, tasks, fileTool, triageTool, cast.KaiActor);
    }

    /// <summary>Files the Request as Mia and returns the file tool's answer.</summary>
    private static Task<string> FileAsync(Scene scene, CancellationToken ct) =>
        scene.FileTool.InvokeAsync(
            new JsonObject
            {
                ["toTeam"] = "Platform",
                ["kind"] = "bug",
                ["title"] = "Broken",
                ["body"] = "## Steps to reproduce\nOpen the page.\n\n## Expected and actual\nIt should load; it does not.",
            },
            ct);

    /// <summary>Filing, accepting and working a Request wakes every recipient exactly once per event.</summary>
    [Fact]
    public async Task Request_FileAcceptWork_WakesEachRecipientOncePerEvent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Scene scene = await CreateSceneAsync(dir, ct);
        using Harness harness = scene.Harness;

        string filed = await FileAsync(scene, ct);
        await SettleAsync(harness);
        string[] afterFile = Recipients(harness, 0);
        int seen = harness.Posts.Count;

        TaskItem filedRequest = Assert.Single(harness.Store.All);
        string id = filedRequest.Id.ToString();
        string accepted = await scene.TriageTool.InvokeAsync(new JsonObject { ["taskId"] = id, ["decision"] = "accept", ["assignee"] = "Kai" }, ct);
        await SettleAsync(harness);
        string[] afterAccept = Recipients(harness, seen);
        seen = harness.Posts.Count;

        TaskItem current = harness.Store.Get(filedRequest.Id) ?? throw new InvalidOperationException("Request vanished.");
        _ = scene.Tasks.Update(current.Id, new TaskPatch { Status = TaskState.InProgress }, current.Version, scene.Kai);
        await SettleAsync(harness);
        string[] afterWork = Recipients(harness, seen);

        Assert.Equal(string.Concat("Filed ", id, " in Platform."), filed);
        Assert.Equal(string.Concat("Accepted ", id, "; assigned to Kai."), accepted);
        Assert.Equal(["Nova"], afterFile);
        Assert.Equal(["Kai", "Mia"], afterAccept);
        Assert.Equal(["Mia", "Nova"], afterWork);
    }

    /// <summary>Declining a Request wakes the requesting manager once and the Chief of Staff once.</summary>
    [Fact]
    public async Task Request_Decline_WakesRequestingManagerAndChiefOfStaffOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        Scene scene = await CreateSceneAsync(dir, ct);
        using Harness harness = scene.Harness;

        _ = await FileAsync(scene, ct);
        await SettleAsync(harness);
        int seen = harness.Posts.Count;

        string id = Assert.Single(harness.Store.All).Id.ToString();
        string declined = await scene.TriageTool.InvokeAsync(new JsonObject { ["taskId"] = id, ["decision"] = "decline", ["reason"] = "Not ours." }, ct);
        await SettleAsync(harness);

        Assert.Equal(string.Concat("Declined ", id, "."), declined);
        Assert.Equal(["Alfred", "Mia"], Recipients(harness, seen));
    }
}
