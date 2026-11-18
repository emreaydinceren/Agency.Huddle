using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Hosting;
using Agency.Huddle.Acp.Tests.Fakes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Task 80 (Phase 4 end-to-end, Gate 4): a Request round trip between two Development Teams and a retro walked together
/// through the real host (<see cref="TeamWebApplicationFactory"/>) with a mock ACP adapter as the only adapter. Each test builds
/// its own host, creates the Web Team (Small preset plus the Retro agent) and the API Team (Small preset) through
/// <see cref="HireService"/>, builds the agent tools the way the host does (<see cref="ActivatorUtilities"/> with the caller's
/// user id) and follows one flow. Wakes are counted from the Messages the real <see cref="TaskTriggerService"/> posts.
/// </summary>
public sealed class TeamsPhase4EndToEndTests
{
    private const string WebTeam = "Web";

    private const string ApiTeam = "API";

    private const string TypeId = "development";

    private static readonly string[] StartRoles = ["development-manager", "developer", "reviewer", "qa-engineer"];

    private const string ManagerRole = "development-manager";

    private const string DeveloperRole = "developer";

    private const string ReviewerRole = "reviewer";

    private const string QaRole = "qa-engineer";

    private const string RetroRole = "retro-agent";

    private const string CheckCommand = "dotnet test";

    private const string BugBody = "## Steps to reproduce\nCall GET /login twice.\n\n## Expected and actual\nExpected 200, got 500.";

    /// <summary>The change-log line only the unblock wake (RQ-6) carries. The blocked Task's own earlier coalesced wakes (its creation and the <c>blocked_by</c> edit) fire about a second after their steps, so counting every wake to its assignee races them.</summary>
    private const string UnblockedMarker = "Unblocked: ";

    private const string OwnedRefusal = "Only managers file Requests; ask your own manager.";

    /// <summary>Flow 1 (RQ-1 to RQ-6): Web's manager files a Bug Request to API, API's manager accepts, API's developer works it to Review, API's QA attaches Evidence and moves it to Done, Web's QA confirms and it closes with <c>confirmed_by</c>, and Web's blocked Task wakes at Done.</summary>
    [Fact]
    public async Task Request_RoundTrip_FiledAcceptedWorkedDoneConfirmedClosed_BlockedTaskWakes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor human = HumanActor(run);
        TaskActor webManager = await teams.Web.ActorAsync(run, ManagerRole, ct);
        TaskActor webDeveloper = await teams.Web.ActorAsync(run, DeveloperRole, ct);
        TaskActor webQa = await teams.Web.ActorAsync(run, QaRole, ct);
        TaskActor apiManager = await teams.Api.ActorAsync(run, ManagerRole, ct);
        TaskActor apiDeveloper = await teams.Api.ActorAsync(run, DeveloperRole, ct);
        TaskActor apiQa = await teams.Api.ActorAsync(run, QaRole, ct);
        TaskItem blocked = Saved(service.Create(new TaskDraft("Ship login", WebTeam, null, TaskState.ToDo, Assignee: webDeveloper.Name), human));

        string filed = await Tool<FileRequestTool>(run, webManager).InvokeAsync(FileArguments(blocked.Id.ToString()), ct);

        TaskItem request = Assert.Single(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));
        Assert.Equal($"Filed {request.Id} in {ApiTeam}.", filed);
        Assert.Equal(TaskState.Backlog, request.Status);
        Assert.Equal(TriageState.None, FieldsOf(request).Triage);
        Assert.Equal([request.Id], store.Get(blocked.Id)?.BlockedBy);
        Assert.Equal(1, run.Wakes(apiManager.Name, "Request event: Filed"));

        string accepted = await Tool<TriageRequestTool>(run, apiManager).InvokeAsync(Triage(request.Id, "accept", null, apiDeveloper.Name), ct);

        Assert.Equal($"Accepted {request.Id}; assigned to {apiDeveloper.Name}.", accepted);
        TaskItem working = Current(store, request.Id);
        Assert.Equal(TaskState.ToDo, working.Status);
        Assert.Equal(TriageState.Accepted, FieldsOf(working).Triage);
        Assert.Equal(apiDeveloper.Name, working.Assignee);
        Assert.Equal(1, run.Wakes(webManager.Name, "Request event: Accept"));

        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.InProgress }, null, apiDeveloper));
        await Eventually.UntilAsync(() => run.WakesOf(webManager.Name, apiDeveloper.Name) >= 1, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50), describe: run.Describe);

        await ReviewAsync(run, teams.Api, service, request.Id, ct);
        _ = Saved(service.AddEvidence(request.Id, new EvidenceLine(DateTimeOffset.UtcNow, apiQa.Name, CheckCommand, 0, null, null, string.Empty), apiQa));
        AcceptNamedMerge(service, store, request.Id, HumanActor(run));
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.Done }, null, apiQa));
        await Eventually.UntilAsync(() => run.WakesOf(webManager.Name, apiQa.Name) >= 1 && run.Wakes(webDeveloper.Name, UnblockedMarker) >= 1, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50), describe: run.Describe);

        Assert.Equal(TaskState.Done, Current(store, request.Id).Status);
        Assert.Equal(1, run.Wakes(webDeveloper.Name, UnblockedMarker));

        string confirmed = await Tool<ConfirmRequestTool>(run, webQa).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString(), ["note"] = "Checked on staging." }, ct);

        Assert.Equal($"Confirmed {request.Id}; it is closed.", confirmed);
        TaskItem closed = Current(store, request.Id);
        Assert.True(closed.Location.Closed);
        Assert.Equal(webQa.Name, FieldsOf(closed).ConfirmedBy);
        Assert.Equal(1, run.Wakes(apiManager.Name, "Request event: Confirm"));
        // #539: the hired QA holds the Skill that grants confirm_request, and not the manager's team-management.
        Assert.Contains("request-confirmation", SkillsOf(run, webQa.Name));
        Assert.DoesNotContain("team-management", SkillsOf(run, webQa.Name));
    }

    /// <summary>
    /// #350 item 6, no Action from an Agent: Huddle names the merge when the Proof completes (#416), and the Human's Accept is the
    /// one Human move. The requesting Team's manager is told when the Request reaches Done. In the development Team type
    /// the manager's role does NOT cover verify, so the manager's own <c>confirm_request</c> is refused and the Team's QA confirms
    /// it; the Request closes with no further Human move. Before Done the call is refused with the next step.
    /// </summary>
    [Fact]
    public async Task Request_DoneWakesRequestingManager_WhoConfirmsAndClosesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor human = HumanActor(run);
        TaskActor webManager = await teams.Web.ActorAsync(run, ManagerRole, ct);
        TaskActor webDeveloper = await teams.Web.ActorAsync(run, DeveloperRole, ct);
        TaskActor apiManager = await teams.Api.ActorAsync(run, ManagerRole, ct);
        TaskActor apiDeveloper = await teams.Api.ActorAsync(run, DeveloperRole, ct);
        TaskActor apiQa = await teams.Api.ActorAsync(run, QaRole, ct);
        TaskItem blocked = Saved(service.Create(new TaskDraft("Ship login", WebTeam, null, TaskState.ToDo, Assignee: webDeveloper.Name), human));
        _ = await Tool<FileRequestTool>(run, webManager).InvokeAsync(FileArguments(blocked.Id.ToString()), ct);
        TaskItem request = Assert.Single(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));
        _ = await Tool<TriageRequestTool>(run, apiManager).InvokeAsync(Triage(request.Id, "accept", null, apiDeveloper.Name), ct);
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.InProgress }, null, apiDeveloper));

        string early = await Tool<ConfirmRequestTool>(run, webManager).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString() }, ct);

        Assert.Equal($"A Request can be confirmed once it is in Done. This one is in In Progress. Ask its assignee, {apiDeveloper.Name}, to move it to Done.", early);

        await ReviewAsync(run, teams.Api, service, request.Id, ct);
        _ = Saved(service.AddEvidence(request.Id, new EvidenceLine(DateTimeOffset.UtcNow, apiQa.Name, CheckCommand, 0, null, null, string.Empty), apiQa));
        AcceptNamedMerge(service, store, request.Id, HumanActor(run));
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.Done }, null, apiQa));
        TaskActor webQaNamed = await teams.Web.ActorAsync(run, QaRole, ct);
        await Eventually.UntilAsync(() => run.Wakes(webManager.Name, $"yours: {webQaNamed.Name}.") >= 1, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50), describe: run.Describe);

        string byManager = await Tool<ConfirmRequestTool>(run, webManager).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString() }, ct);
        TaskActor webQa = await teams.Web.ActorAsync(run, QaRole, ct);
        string byQa = await Tool<ConfirmRequestTool>(run, webQa).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString() }, ct);

        Assert.Equal("Only the requesting Team's QA can confirm a Request.", byManager);
        Assert.Equal($"Confirmed {request.Id}; it is closed.", byQa);
        TaskItem closed = Current(store, request.Id);
        Assert.True(closed.Location.Closed);
        Assert.Equal(webQa.Name, FieldsOf(closed).ConfirmedBy);
    }

    /// <summary>
    /// #350 item 6, with an Action: file, triage, work, Review with Evidence carrying an exit code. QA's Evidence alone leaves the
    /// Request in Review, because the Definition of Done's Review item is open (#537); with the reviewer's Evidence Huddle moves it to
    /// Needs You, the Human Accepts, the assignee moves it to Done, the Team's QA confirms and it is closed. The Human's only
    /// move on the Request is Accept.
    /// </summary>
    [Fact]
    public async Task Request_WithAction_NeedsYouAcceptDoneConfirmed_HumanOnlyAccepts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor human = HumanActor(run);
        TaskActor webManager = await teams.Web.ActorAsync(run, ManagerRole, ct);
        TaskActor webQa = await teams.Web.ActorAsync(run, QaRole, ct);
        TaskActor apiManager = await teams.Api.ActorAsync(run, ManagerRole, ct);
        TaskActor apiDeveloper = await teams.Api.ActorAsync(run, DeveloperRole, ct);
        TaskActor apiQa = await teams.Api.ActorAsync(run, QaRole, ct);
        _ = await Tool<FileRequestTool>(run, webManager).InvokeAsync(FileArguments(null), ct);
        TaskItem request = Assert.Single(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));
        _ = await Tool<TriageRequestTool>(run, apiManager).InvokeAsync(Triage(request.Id, "accept", null, apiDeveloper.Name), ct);
        _ = Saved(service.Update(request.Id, new TaskPatch { Action = Optional<string?>.Set("Deploy the fix to production") }, null, apiManager));
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.InProgress }, null, apiDeveloper));

        _ = Saved(service.AddEvidence(request.Id, new EvidenceLine(DateTimeOffset.UtcNow, apiQa.Name, CheckCommand, 0, null, null, string.Empty), apiQa));

        Assert.Equal(TaskState.InProgress, Current(store, request.Id).Status);

        await ReviewAsync(run, teams.Api, service, request.Id, ct);

        Assert.Equal(TaskState.NeedsYou, Current(store, request.Id).Status);
        TaskItem waiting = Current(store, request.Id);
        _ = Saved(service.Decide(request.Id, Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Accept, null, waiting.Version, human));
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.Done }, null, apiDeveloper));
        string confirmed = await Tool<ConfirmRequestTool>(run, webQa).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString() }, ct);

        Assert.Equal($"Confirmed {request.Id}; it is closed.", confirmed);
        TaskItem closed = Current(store, request.Id);
        Assert.True(closed.Location.Closed);
        ChangeLogEntry humanMove = Assert.Single(closed.ChangeLog, entry => string.Equals(entry.Actor, human.Name, StringComparison.Ordinal));
        Assert.StartsWith("accepted ", humanMove.Summary, StringComparison.Ordinal); // contains-ok: the rest of the entry is a content fingerprint of the Request text
    }

    /// <summary>The Team's reviewer posts a passing check run, which the development Definition of Done's Review item needs (#537).</summary>
    /// <param name="run">The running host.</param>
    /// <param name="team">The Team that holds the Request.</param>
    /// <param name="service">Adds the Evidence.</param>
    /// <param name="id">The Request.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>A task that completes when the Evidence is saved.</returns>
    private static async Task ReviewAsync(Run run, TeamCast team, TaskService service, TaskId id, CancellationToken ct)
    {
        TaskActor reviewer = await team.ActorAsync(run, ReviewerRole, ct);
        _ = Saved(service.AddEvidence(id, new EvidenceLine(DateTimeOffset.UtcNow, reviewer.Name, CheckCommand, 0, null, null, "Approved."), reviewer));
    }

    /// <summary>The Human's Accept on a Request Huddle brought to Needs You with the merge named (#416), checking that it did.</summary>
    /// <param name="service">Makes the decision.</param>
    /// <param name="store">Reads the Task.</param>
    /// <param name="id">The Request.</param>
    /// <param name="human">The Human's actor.</param>
    private static void AcceptNamedMerge(TaskService service, TaskStore store, TaskId id, TaskActor human)
    {
        TaskItem waiting = Current(store, id);
        Assert.Equal(TaskState.NeedsYou, waiting.Status);
        Assert.Equal(Agency.Huddle.App.Tasks.HumanGate.HumanGateRules.MergeAction, waiting.Action);
        _ = Saved(service.Decide(id, Agency.Huddle.App.Tasks.HumanGate.TaskDecision.Accept, null, waiting.Version, human));
    }

    /// <summary>
    /// #350 item 4 and 6, the Bea case: a small-business manager covers <c>verify</c>, so when the development Team finishes the
    /// Request she filed, her own <c>confirm_request</c> is accepted and closes it, with no Human move after the Accept.
    /// </summary>
    [Fact]
    public async Task Request_FiledBySmallBusinessManager_ManagerConfirmsAndClosesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir apiSpace = NewWorkspace();
        using TempDataDir shopSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        TeamCast api = await CreateAndStartAsync(run, ApiTeam, apiSpace.Path, [], ct);
        TeamCast shop = await CreateAndStartAsync(run, "Bakery", shopSpace.Path, [], ct, "small-business", ["business-manager"]);
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor bea = await shop.ActorAsync(run, "business-manager", ct);
        TaskActor apiManager = await api.ActorAsync(run, ManagerRole, ct);
        TaskActor apiDeveloper = await api.ActorAsync(run, DeveloperRole, ct);
        TaskActor apiQa = await api.ActorAsync(run, QaRole, ct);
        _ = await Tool<FileRequestTool>(run, bea).InvokeAsync(FileArguments(null), ct);
        TaskItem request = Assert.Single(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));
        _ = await Tool<TriageRequestTool>(run, apiManager).InvokeAsync(Triage(request.Id, "accept", null, apiDeveloper.Name), ct);
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.InProgress }, null, apiDeveloper));
        await ReviewAsync(run, api, service, request.Id, ct);
        _ = Saved(service.AddEvidence(request.Id, new EvidenceLine(DateTimeOffset.UtcNow, apiQa.Name, CheckCommand, 0, null, null, string.Empty), apiQa));
        AcceptNamedMerge(service, store, request.Id, HumanActor(run));
        _ = Saved(service.Update(request.Id, new TaskPatch { Status = TaskState.Done }, null, apiQa));

        string confirmed = await Tool<ConfirmRequestTool>(run, bea).InvokeAsync(new JsonObject { ["taskId"] = request.Id.ToString() }, ct);

        Assert.Equal($"Confirmed {request.Id}; it is closed.", confirmed);
        Assert.True(Current(store, request.Id).Location.Closed);
        // #539: a manager whose role covers verify is hired with the Skill that grants confirm_request.
        Assert.Contains("request-confirmation", SkillsOf(run, bea.Name));
    }

    /// <summary>Flow 2 (RQ-3): declining a Request with a reason moves it to Rejected and wakes the requesting manager and the Chief of Staff.</summary>
    [Fact]
    public async Task Request_Declined_MovesToRejected_WakesRequestingManagerAndChiefOfStaff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor webManager = await teams.Web.ActorAsync(run, ManagerRole, ct);
        TaskActor apiManager = await teams.Api.ActorAsync(run, ManagerRole, ct);
        string chief = run.Services.GetRequiredService<PersonaStore>().Entries
            .First(static entry => string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal)).Name;
        _ = await run.Services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(chief, null, ct);
        _ = await Tool<FileRequestTool>(run, webManager).InvokeAsync(FileArguments(null), ct);
        TaskItem request = Assert.Single(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));

        string declined = await Tool<TriageRequestTool>(run, apiManager).InvokeAsync(Triage(request.Id, "decline", "The login service is retired.", null), ct);

        Assert.Equal($"Declined {request.Id}.", declined);
        TaskItem rejected = Current(store, request.Id);
        Assert.Equal(TaskState.Rejected, rejected.Status);
        Assert.Equal(TriageState.Declined, FieldsOf(rejected).Triage);
        Assert.Equal("The login service is retired.", FieldsOf(rejected).DeclineReason);
        Assert.Equal(1, run.Wakes(webManager.Name, "Request event: Decline"));
        Assert.Equal(1, run.Wakes(chief, "Request event: Decline"));
    }

    /// <summary>Flow 3 (RQ-2): a specialist calling <c>file_request</c> is refused, nothing is filed, and only the manager holds the <c>team-management</c> Skill that grants the tool.</summary>
    [Fact]
    public async Task FileRequest_BySpecialist_IsRefused_AndOnlyTheManagerHoldsTheSkill()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        TaskActor webDeveloper = await teams.Web.ActorAsync(run, DeveloperRole, ct);

        string refused = await Tool<FileRequestTool>(run, webDeveloper).InvokeAsync(FileArguments(null), ct);

        Assert.Equal(OwnedRefusal, refused);
        Assert.DoesNotContain(store.All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal));
        Assert.Contains("team-management", SkillsOf(run, teams.Web.Card(ManagerRole).Name));
        Assert.DoesNotContain("team-management", SkillsOf(run, webDeveloper.Name));
    }

    /// <summary>Flow 4 (RT-3 to RT-7): three Review bounces show in <c>read_records</c>, the Retro agent's report is written, one <c>Process.md</c> change is proposed, Accept applies it exactly once and registers a Done row, and the next retro verifies the row only with a link.</summary>
    [Fact]
    public async Task Retro_BouncesToReport_ProposalAppliedOnce_RowDone_VerifiedOnlyWithLink()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        await using Run run = Run.Open(root);
        Teams teams = await StartTeamsAsync(run, webSpace.Path, apiSpace.Path, ct);
        TaskActor retro = await teams.Web.ActorAsync(run, RetroRole, ct);
        TaskItem bounced = await SeedBouncesAsync(run, teams.Web, ct);

        string records = await Tool<ReadRecordsTool>(run, retro).InvokeAsync(new JsonObject { ["team"] = WebTeam }, ct);

        Assert.Equal("review bounces: 3", Assert.Single(records.Split('\n').Select(static line => line.TrimEnd('\r')), static line => line.StartsWith("review bounces:", StringComparison.Ordinal)));
        string reportPath = run.Services.GetRequiredService<RetroFileStore>().WriteReport(WebTeam, new RetroReport(
            new DateOnly(2026, 10, 8),
            [new RetroIssue("Work bounces from Review", "Three bounces on one Task.", [bounced.Id.ToString()])],
            []));
        Assert.Equal(Path.Combine(run.TeamFolder(WebTeam), TeamPaths.RetrosDir, "2026-10-08.md"), reportPath);
        Assert.True(File.Exists(reportPath));

        string sent = await Tool<ProposeRetroChangesTool>(run, retro).InvokeAsync(ProposeArguments(("Explain bounces", "## When it applies", "## Retro change\n\n- Explain a Review bounce on the Task.\n\n## When it applies")), ct);

        Assert.Equal("Proposal sent to the Human.", sent);
        Room room = await WebTeamRoomAsync(run, ct);
        RetroProposalService proposals = run.Services.GetRequiredService<RetroProposalService>();
        RetroProposalSet set = Assert.Single(await proposals.PendingAsync(room.Id, ct));
        string before = File.ReadAllText(Path.Combine(run.TeamFolder(WebTeam), TeamPaths.AgentsMdFileName));

        RetroChangeOutcome applied = await proposals.AcceptAsync(room.Id, set.Id, "1", ct);
        RetroChangeOutcome again = await proposals.AcceptAsync(room.Id, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Applied, applied.Kind);
        Assert.Equal(RetroChangeOutcomeKind.Gone, again.Kind);
        string after = File.ReadAllText(Path.Combine(run.TeamFolder(WebTeam), TeamPaths.AgentsMdFileName));
        Assert.Equal(1, RetroDiff.CountMatches(after, "- Explain a Review bounce on the Task."));
        Assert.Equal(0, RetroDiff.CountMatches(before, "- Explain a Review bounce on the Task."));
        RetroFileStore register = run.Services.GetRequiredService<RetroFileStore>();
        RegisterRow row = Assert.Single(register.ReadRegister(WebTeam));
        Assert.Equal("R-001", row.Id);
        Assert.Equal(RetroStatus.Done, row.Status);
        Assert.Equal(["R-001"], RetroVerifier.Due(register.ReadRegister(WebTeam)).Select(static prompt => prompt.RowId));

        MarkRetroRowTool mark = Tool<MarkRetroRowTool>(run, retro);
        string opinion = await mark.InvokeAsync(MarkArguments("R-001", "It feels better."), ct);
        string linked = await mark.InvokeAsync(MarkArguments("R-001", bounced.Id.ToString()), ct);

        Assert.Equal("Verified needs evidence from after the change.", opinion);
        Assert.Equal("Row R-001 is now Verified.", linked);
        Assert.Equal(RetroStatus.Verified, Assert.Single(register.ReadRegister(WebTeam)).Status);
        Assert.Equal(TaskState.InProgress, Current(run.Services.GetRequiredService<TaskStore>(), bounced.Id).Status);
    }

    /// <summary>Flow 5 (NF-8): a new host on the same data directory still has the Request, the retro register and the proposal still waiting, and the waiting change can still be accepted.</summary>
    [Fact]
    public async Task Restart_KeepsRequests_Register_AndPendingProposals()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir webSpace = NewWorkspace();
        using TempDataDir apiSpace = NewWorkspace();
        await using TeamWebApplicationFactory root = new();
        TaskId requestId;
        string roomId;
        string setId;
        await using (Run first = Run.Open(root))
        {
            Teams teams = await StartTeamsAsync(first, webSpace.Path, apiSpace.Path, ct);
            TaskActor webManager = await teams.Web.ActorAsync(first, ManagerRole, ct);
            TaskActor retro = await teams.Web.ActorAsync(first, RetroRole, ct);
            _ = await Tool<FileRequestTool>(first, webManager).InvokeAsync(FileArguments(null), ct);
            requestId = Assert.Single(first.Services.GetRequiredService<TaskStore>().All, static task => string.Equals(task.Location.Team, ApiTeam, StringComparison.Ordinal)).Id;
            _ = await Tool<ProposeRetroChangesTool>(first, retro).InvokeAsync(
                ProposeArguments(("First", "## When it applies", "## When it applies now"), ("Second", "## Who does what", "## Who does what now")),
                ct);
            Room room = await WebTeamRoomAsync(first, ct);
            roomId = room.Id;
            RetroProposalService proposals = first.Services.GetRequiredService<RetroProposalService>();
            setId = Assert.Single(await proposals.PendingAsync(roomId, ct)).Id;
            Assert.Equal(RetroChangeOutcomeKind.Applied, (await proposals.AcceptAsync(roomId, setId, "1", ct)).Kind);
        }

        await using Run second = Run.Open(root);
        ITeamCatalog catalog = second.Services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(WebTeam)?.Members.Count == 5 && catalog.Find(ApiTeam)?.Members.Count == 4, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));

        TaskItem request = Current(second.Services.GetRequiredService<TaskStore>(), requestId);
        Assert.Equal(TaskState.Backlog, request.Status);
        Assert.Equal(WebTeam, FieldsOf(request).FromTeam);
        RegisterRow row = Assert.Single(second.Services.GetRequiredService<RetroFileStore>().ReadRegister(WebTeam));
        Assert.Equal(RetroStatus.Done, row.Status);
        RetroProposalService reopened = second.Services.GetRequiredService<RetroProposalService>();
        RetroProposalSet pending = Assert.Single(await reopened.PendingAsync(roomId, ct));
        RetroChange waiting = Assert.Single(pending.Changes);
        Assert.Equal("Second", waiting.Title);
        Assert.Equal(RetroChangeOutcomeKind.Applied, (await reopened.AcceptAsync(roomId, setId, waiting.Id, ct)).Kind);
        Assert.Equal(["R-001", "R-002"], second.Services.GetRequiredService<RetroFileStore>().ReadRegister(WebTeam).Select(static each => each.Id));
    }

    /// <summary>Waits for the reconciler to create the Web Team's Team Room, the only room the retro card shows in (ADR-0052).</summary>
    /// <param name="run">The running host.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The Team Room.</returns>
    private static async Task<Room> WebTeamRoomAsync(Run run, CancellationToken ct)
    {
        ITeamDirectory directory = run.Services.GetRequiredService<ITeamDirectory>();
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        while (System.Diagnostics.Stopwatch.GetElapsedTime(start) < TestBudgets.Slowest)
        {
            if (await directory.FindTeamRoomAsync(WebTeam, ct) is { } found)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }

        throw new InvalidOperationException("The Web Team has no Team Room.");
    }

    /// <summary>Creates a temp folder that looks like a repository, for the Development type's required workspace.</summary>
    /// <returns>The folder, deleted on dispose.</returns>
    private static TempDataDir NewWorkspace()
    {
        TempDataDir workspace = new();
        _ = Directory.CreateDirectory(Path.Combine(workspace.Path, ".git"));
        return workspace;
    }

    /// <summary>Unwraps a saved Task result.</summary>
    /// <param name="result">The result of a Task service call.</param>
    /// <returns>The saved Task.</returns>
    private static TaskItem Saved(TaskResult result) => Assert.IsType<TaskResult.Saved>(result).Task;

    /// <summary>Reads a Task from the store.</summary>
    /// <param name="store">The Task store.</param>
    /// <param name="id">The Task id.</param>
    /// <returns>The Task as stored now.</returns>
    private static TaskItem Current(TaskStore store, TaskId id) => store.Get(id) ?? throw new InvalidOperationException("The Task is not in the store.");

    /// <summary>Reads the Request fields of a Task.</summary>
    /// <param name="task">A Request.</param>
    /// <returns>Its fields.</returns>
    private static RequestFields FieldsOf(TaskItem task)
    {
        Dictionary<string, string> frontmatter = new(task.UnknownFields, StringComparer.Ordinal);
        Assert.True(RequestFields.TryRead(frontmatter, out RequestFields? fields));
        return fields ?? throw new InvalidOperationException("Not a Request.");
    }

    /// <summary>The Skill names the stored Agent holds.</summary>
    /// <param name="run">The running host.</param>
    /// <param name="name">The Agent's name.</param>
    /// <returns>The Skill names.</returns>
    private static IReadOnlyList<string> SkillsOf(Run run, string name)
    {
        Persona persona = run.Services.GetRequiredService<PersonaStore>().Get(name) ?? throw new InvalidOperationException("No such Agent.");
        Assert.True(PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _));
        return identity?.Skills ?? [];
    }

    /// <summary>Gets the Human's Task actor.</summary>
    /// <param name="run">The running host.</param>
    /// <returns>The Human actor.</returns>
    private static TaskActor HumanActor(Run run) => TaskActors.Human(run.Services.GetRequiredService<IOptions<TeamOptions>>().Value);

    /// <summary>Builds an agent tool the way the host does: dependencies from the container, the caller's user id last.</summary>
    /// <typeparam name="TTool">The tool type.</typeparam>
    /// <param name="run">The running host.</param>
    /// <param name="caller">The calling Agent.</param>
    /// <returns>The tool.</returns>
    private static TTool Tool<TTool>(Run run, TaskActor caller)
        where TTool : class => ActivatorUtilities.CreateInstance<TTool>(run.Services, caller.UserId ?? string.Empty);

    /// <summary>Builds the arguments of a bug Request from Web to API.</summary>
    /// <param name="relatedTaskId">The Web Task that waits on the answer, or <see langword="null"/>.</param>
    /// <returns>The call arguments.</returns>
    private static JsonObject FileArguments(string? relatedTaskId)
    {
        JsonObject arguments = new() { ["toTeam"] = ApiTeam, ["kind"] = "bug", ["title"] = "Login returns 500", ["body"] = BugBody };
        if (relatedTaskId is not null)
        {
            arguments["relatedTaskId"] = relatedTaskId;
        }

        return arguments;
    }

    /// <summary>Builds the arguments of a triage call.</summary>
    /// <param name="id">The Request.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="reason">The reason, or <see langword="null"/>.</param>
    /// <param name="assignee">The assignee, or <see langword="null"/>.</param>
    /// <returns>The call arguments.</returns>
    private static JsonObject Triage(TaskId id, string decision, string? reason, string? assignee)
    {
        JsonObject arguments = new() { ["taskId"] = id.ToString(), ["decision"] = decision };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (assignee is not null)
        {
            arguments["assignee"] = assignee;
        }

        return arguments;
    }

    /// <summary>Builds the arguments of a <c>propose_retro_changes</c> call that edits Web's <c>Process.md</c>.</summary>
    /// <param name="changes">Each change's title, text to find and replacement.</param>
    /// <returns>The call arguments.</returns>
    private static JsonObject ProposeArguments(params (string Title, string Find, string Replace)[] changes)
    {
        JsonArray items = [];
        foreach ((string title, string find, string replace) in changes)
        {
            items.Add(new JsonObject { ["title"] = title, ["targetFile"] = TeamPaths.AgentsMdFileName, ["find"] = find, ["replace"] = replace });
        }

        return new JsonObject { ["team"] = WebTeam, ["proposals"] = items };
    }

    /// <summary>Builds the arguments of a <c>mark_retro_row</c> call that asks for Verified.</summary>
    /// <param name="rowId">The register row.</param>
    /// <param name="evidence">The evidence text.</param>
    /// <returns>The call arguments.</returns>
    private static JsonObject MarkArguments(string rowId, string evidence) =>
        new() { ["team"] = WebTeam, ["rowId"] = rowId, ["status"] = "Verified", ["evidence"] = evidence };

    /// <summary>Creates one Web Task and bounces it from Review to To Do three times, leaving it in Review.</summary>
    /// <param name="run">The running host.</param>
    /// <param name="web">The Web Team's cast.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>The Task as stored after the last move.</returns>
    private static async Task<TaskItem> SeedBouncesAsync(Run run, TeamCast web, CancellationToken ct)
    {
        TaskService service = run.Services.GetRequiredService<TaskService>();
        TaskActor developer = await web.ActorAsync(run, DeveloperRole, ct);
        TaskActor reviewer = await web.ActorAsync(run, ReviewerRole, ct);
        TaskItem task = Saved(service.Create(new TaskDraft("Fix checkout", WebTeam, null, TaskState.InProgress, Assignee: developer.Name), HumanActor(run)));
        // Huddle no longer writes the Review bounce markers (D-17), so the three bounces are written the way an older Huddle wrote them.
        TaskStore store = run.Services.GetRequiredService<TaskStore>();
        string text = File.ReadAllText(task.Path);
        for (int round = 0; round < 3; round++)
        {
            text = TaskFileFormat.AppendEntry(text, new ChangeLogEntry(DateTimeOffset.UtcNow.AddMinutes(round), reviewer.Name, "status: Review → To Do"));
        }

        File.WriteAllText(task.Path, text);
        store.RebuildFromWatcher();
        return Current(store, task.Id);
    }

    /// <summary>Creates and starts the Web Team (Small preset plus the Retro agent) and the API Team (Small preset).</summary>
    /// <param name="run">The running host.</param>
    /// <param name="webWorkspace">Web's repository folder.</param>
    /// <param name="apiWorkspace">API's repository folder.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>Both casts.</returns>
    private static async Task<Teams> StartTeamsAsync(Run run, string webWorkspace, string apiWorkspace, CancellationToken ct)
    {
        TeamCast web = await CreateAndStartAsync(run, WebTeam, webWorkspace, [RetroRole], ct);
        TeamCast api = await CreateAndStartAsync(run, ApiTeam, apiWorkspace, [], ct);
        return new Teams(web, api);
    }

    /// <summary>Creates a Development Team with the Small preset plus extra roles, gives it its default process, hires the manager and then the other members the way Build a team does, and onboards the manager.</summary>
    /// <param name="run">The running host.</param>
    /// <param name="teamName">The Team's name.</param>
    /// <param name="workspace">The repository folder.</param>
    /// <param name="extraRoles">Role ids hired on top of the preset.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The hired cards.</returns>
    /// <param name="typeId">The Team type, the development type unless a test needs another shape.</param>
    /// <param name="startRoles">The roles the Team starts with, the development preset unless a test needs another shape.</param>
    private static async Task<TeamCast> CreateAndStartAsync(Run run, string teamName, string workspace, string[] extraRoles, CancellationToken ct, string typeId = TypeId, string[]? startRoles = null)
    {
        IServiceProvider services = run.Services;
        TeamTypeManifest type = services.GetRequiredService<TeamTypeCatalog>().Find(typeId) ?? throw new InvalidOperationException("The Team type is not in the catalog.");
        ITeamFolders folders = services.GetRequiredService<ITeamFolders>();
        HireService hiring = services.GetRequiredService<HireService>();
        IHireContext context = services.GetRequiredService<IHireContext>();
        PersonaStore personas = services.GetRequiredService<PersonaStore>();
        TeamFile file = new(new VersionedRef(type.Id, type.Version), null, workspace, null, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam(teamName, file).Outcome);
        _ = hiring.ProvisionProcess(teamName, null);

        List<string> used = [.. personas.ListNames()];
        List<CardState> cards = [];
        foreach (string roleId in (startRoles ?? StartRoles).Concat(extraRoles))
        {
            RoleSpec role = type.Roles.First(each => string.Equals(each.Id, roleId, StringComparison.Ordinal));
            CardState card = CardResolver.Resolve(role, used, context.Answered, context.TierMap, context.AvoidAdapters, services.GetRequiredService<ISkillAvailability>(), context.Connectors);
            Assert.Equal(CardReadiness.Ready, card.Readiness);
            used.Add(card.Name);
            cards.Add(card);
        }

        CardState manager = cards.First(card => type.Roles.First(role => string.Equals(role.Id, card.RoleId, StringComparison.Ordinal)).Manager);
        HireResult hired = hiring.Hire(new HireRequest(teamName, type.Id, manager.RoleId, manager));
        Assert.Null(hired.Error);
        _ = folders.UpdateTeamFile(teamName, current => current with { Manager = manager.Name });

        foreach (CardState card in cards.Where(card => !ReferenceEquals(card, manager)))
        {
            HireResult hiredMember = hiring.Hire(new HireRequest(teamName, type.Id, card.RoleId, card));
            Assert.Null(hiredMember.Error);
        }

        _ = await hiring.OnboardManagerAsync(teamName, manager.Name, ct);

        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(teamName) is { Manager: not null } team && team.Members.Count == cards.Count, ct, timeout: TestBudgets.Slowest, interval: TimeSpan.FromMilliseconds(50));
        return new TeamCast(cards);
    }

    /// <summary>Both Teams' casts.</summary>
    /// <param name="Web">The Web Team.</param>
    /// <param name="Api">The API Team.</param>
    private sealed record Teams(TeamCast Web, TeamCast Api);

    /// <summary>The cards hired for a Team.</summary>
    /// <param name="Cards">Every hired card.</param>
    private sealed record TeamCast(IReadOnlyList<CardState> Cards)
    {
        /// <summary>Finds the card hired for <paramref name="roleId"/>.</summary>
        /// <param name="roleId">The role id.</param>
        /// <returns>The card.</returns>
        public CardState Card(string roleId) => this.Cards.First(each => string.Equals(each.RoleId, roleId, StringComparison.Ordinal));

        /// <summary>Builds the Task actor for the Agent hired for <paramref name="roleId"/>.</summary>
        /// <param name="run">The running host.</param>
        /// <param name="roleId">The role id.</param>
        /// <param name="ct">Cancels the user lookup.</param>
        /// <returns>The agent actor.</returns>
        public async Task<TaskActor> ActorAsync(Run run, string roleId, CancellationToken ct)
        {
            CardState card = this.Card(roleId);
            User? user = await run.Services.GetRequiredService<ITeamDirectory>().UpsertAgentUserAsync(card.Name, null, ct);
            Assert.NotNull(user);
            return new TaskActor(TaskActorKind.Agent, card.Name, user.Id);
        }
    }

    /// <summary>A host built over the shared factory's data directory, with the mock adapter and a Message recorder installed.</summary>
    private sealed class Run : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> host;

        private Run(WebApplicationFactory<Program> host)
        {
            this.host = host;
            this.Services.GetRequiredService<RoomEvents>().MessagePosted += this.Posts.Enqueue;
        }

        /// <summary>Gets every Message posted since the host started.</summary>
        public ConcurrentQueue<MessagePostedEvent> Posts { get; } = new();

        /// <summary>Gets the host's services.</summary>
        public IServiceProvider Services => this.host.Services;

        /// <summary>Builds and starts a host on <paramref name="root"/>'s data directory.</summary>
        /// <param name="root">The factory that owns the data directory.</param>
        /// <returns>The running host.</returns>
        public static Run Open(TeamWebApplicationFactory root)
        {
            MockLauncher launcher = new();
            WebApplicationFactory<Program> host = root.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Team:Acp:Enabled", "true");
                builder.UseSetting("Team:Acp:Args:0", "--phase4-mock-adapter");
                builder.UseSetting("Team:Tasks:WakeCoalesceSeconds", "1");
                foreach (ModelTier tier in Enum.GetValues<ModelTier>())
                {
                    builder.UseSetting($"Team:Teams:Tiers:claude:{tier}", $"mock-{tier}");
                }

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAgentProcessLauncher>();
                    services.AddSingleton<IAgentProcessLauncher>(launcher);
                    services.RemoveAll<Agency.Huddle.App.SkillSources.ISkillCopier>();
                    services.AddSingleton<Agency.Huddle.App.SkillSources.ISkillCopier>(new FakeSkillCopier());
                    services.AddSingleton<ISkillAvailability>(new FakeSkillAvailability());
                });
            });
            return new Run(host);
        }

        /// <summary>The Team's folder on disk, found the way the host finds it.</summary>
        /// <param name="team">The Team's name.</param>
        /// <returns>The folder path.</returns>
        public string TeamFolder(string team) =>
            this.Services.GetRequiredService<ITeamFolders>().FindTeam(team).Value?.FullPath ?? throw new InvalidOperationException("No such Team folder.");

        /// <summary>Lists the first line of every Task wake Message posted so far, for a failure message.</summary>
        /// <returns>One line per wake.</returns>
        public string Describe() => string.Join('\n', this.Posts
            .Where(static post => post.Message.Text.StartsWith('@'))
            .Select(static post => post.Message.Text.Replace('\n', '|')));

        /// <summary>Counts the Task wake Messages to <paramref name="name"/> that carry <paramref name="marker"/>.</summary>
        /// <param name="name">The woken Agent's name.</param>
        /// <param name="marker">Text the wake Message must contain, such as <c>Request event: Filed</c>.</param>
        /// <returns>The count.</returns>
        public int Wakes(string name, string marker) => this.Posts.Count(post =>
            post.Message.Text.StartsWith($"@{name} Task ", StringComparison.Ordinal)
            && post.Message.Text.Contains(marker, StringComparison.Ordinal));

        /// <summary>Counts the Task wake Messages posted that open with a Mention of <paramref name="name"/>.</summary>
        /// <param name="name">The woken Agent's name.</param>
        /// <param name="changedBy">The actor the wake names as having changed the Task, or <see langword="null"/> for any wake.</param>
        /// <returns>The count.</returns>
        public int WakesOf(string name, string? changedBy) => this.Posts.Count(post =>
            post.Message.Text.StartsWith($"@{name} Task ", StringComparison.Ordinal)
            && (changedBy is null || post.Message.Text.Contains($" was changed by {changedBy}:", StringComparison.Ordinal)));

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            this.Services.GetRequiredService<RoomEvents>().MessagePosted -= this.Posts.Enqueue;
            await this.host.DisposeAsync();
        }
    }

    /// <summary>Starts an in-memory mock agent for every launch, one per Agent host.</summary>
    private sealed class MockLauncher : IAgentProcessLauncher
    {
        /// <inheritdoc />
        public IAgentProcess Launch(AgentProcessOptions options) => new FakeAgentProcessLauncher().Launch(options);
    }
}
