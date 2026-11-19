using Microsoft.Extensions.Logging;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// One wake rule failing must not stop the others (S-B2): <see cref="TaskTriggerService"/> logs the rule by name at Warning, skips it and
/// runs the rules after it. The failure is made by a real rule over a throwing collaborator (S-B3): the Human lookup throws for the
/// Platform manager's Name, which only the manager rule asks about.
/// </summary>
public sealed class TaskTriggerRuleIsolationTests
{
    private const string Probe = "{{assignee}}|{{taskId}}|{{actor}}|{{changes}}";

    /// <summary>The manager rule throws; the assignee before it and the requesting manager after it are still woken, and the throw is logged by rule name.</summary>
    [Fact]
    public async Task Fire_ManagerRuleThrows_LogsAndStillWakesTheOthers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams =
            [
                new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
                new TeamSummary("Design", [], ["Mia"], true) { Manager = "Mia" },
            ],
        };
        using Harness harness = await CreateHarnessAsync(dir, new TeamOptions { HumanName = "You" }, ct, catalog, human: new ThrowingHumanIdentity("Nova"));
        Cast cast = await StartWakingAsync(harness, ct);
        _ = harness.Personas.Add(new PersonaIdentity("Mia", "Mia", "mia", ["Design"]), "You are Mia.");
        User? mia = await harness.Directory.UpsertAgentUserAsync("Mia", null, ct);
        Assert.NotNull(mia);
        Room room = await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, mia.Id], ct);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        TaskItem request = harness.Seed(TestTasks.Make(
            id: "PLAT-0001",
            assignee: "Kai",
            originRoomId: room.Id,
            unknownFields: [.. new RequestFields(RequestKind.Bug, "Design", "Mia", TriageState.None, string.Empty, string.Empty).ToFrontmatter()]));

        harness.Events.RaiseTaskChanged(new TaskChange(
            request,
            request,
            [new FieldChange(TaskField.Status, "In Progress", "Done")],
            HumanActor,
            new ChangeLogEntry(harness.Clock.GetUtcNow(), HumanActor.Name, "Started")));
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();

        Assert.Equal(
            ["Kai|PLAT-0001|You|- Started", "Mia|PLAT-0001|You|- Started"],
            harness.Posts.Select(p => p.Message.Text).ToArray());
        Assert.Equal(
            ["Wake rule Manager failed for Task PLAT-0001 and was skipped."],
            harness.Logger.AtExactly(LogLevel.Warning).Select(e => e.Message).ToArray());
    }
}
