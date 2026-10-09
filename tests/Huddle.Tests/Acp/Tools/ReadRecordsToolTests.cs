using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="ReadRecordsTool"/> (Task 74, RT-3): the Retro agent's bundle, Task and Room transcript reads, who may call
/// it, and the output cap.
/// </summary>
public sealed class ReadRecordsToolTests
{
    private const string Team = "Platform";
    private const string RoomId = "room-1";

    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "retroRole": "retro",
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "retro", "title": "Retro", "candidate": "candidates/retro.md", "maxPerTeam": 1 }
          ] }
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Mia manages Platform, Rae is its Retro agent, Sam is a member holding the Skill, Zed a member without it; Ola is not a member.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly TempDataDir libraryDir = new();

        /// <summary>The Task tool harness.</summary>
        public required TaskToolHarness Harness { get; init; }

        /// <summary>The caller ids by name.</summary>
        public Dictionary<string, string> Ids { get; } = new(StringComparer.Ordinal);

        /// <summary>The clock the tool reads.</summary>
        public ManualTimeProvider Clock { get; } = new(Now);

        /// <summary>The chat store the tool reads transcripts from.</summary>
        public required FileChatStore Chat { get; init; }

        /// <summary>The folder of Platform's Library Team folder.</summary>
        public string TeamFolder => Path.Combine(this.libraryDir.Path, Team);

        /// <summary>Builds the tool for a caller.</summary>
        /// <param name="caller">The caller's name.</param>
        public ReadRecordsTool Tool(string caller)
        {
            FakeTeamCatalog catalog = new()
            {
                Teams = [new TeamSummary(Team, [], ["Mia", "Rae", "Sam", "Zed", "Ke/vin"], true) { TeamType = new VersionedRef("web", "1.2.0"), Manager = "Mia" }],
            };
            Dictionary<string, string> builtins = new(StringComparer.Ordinal)
            {
                ["web/team-type.json"] = Manifest,
                ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
                ["web/candidates/retro.md"] = "---\ntitle: Retro\nconsult_when: retros\n---\nYou review.",
                ["web/processes/flow.md"] = "# Flow",
            };
            TeamTypeCatalog types = new(this.Harness.Options, NullLogger<TeamTypeCatalog>.Instance, builtins);
            LibraryRoot root = new("teams", "Teams", this.libraryDir.Path, LibraryRootKind.Teams);
            FakeTeamFolders folders = new()
            {
                TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(root, Team, this.TeamFolder, LibraryNodeRole.TeamFolder)),
            };
            RecordsBundleBuilder builder = new(this.Harness.Store, catalog, new PersonaSpendStore(this.Harness.Options), this.Harness.Options);
            return new ReadRecordsTool(catalog, this.Harness.Store, this.Harness.Personas, types, builder, this.Chat, folders, this.Harness.Directory, this.Clock, this.Ids[caller], new FakePromptSource());
        }

        /// <summary>Writes a Platform Task whose Change log has one entry at <paramref name="at"/>.</summary>
        /// <param name="id">The Task id.</param>
        /// <param name="at">When the entry was made.</param>
        /// <param name="summary">The Change log summary.</param>
        /// <param name="description">The Task body.</param>
        /// <param name="team">The Task's Team.</param>
        public void Seed(string id, DateTimeOffset at, string summary, string description = "Body", string team = Team)
        {
            TaskItem task = TestTasks.Make(id: id, status: TaskState.Review, description: description, location: new TaskLocation(team, null, false), changeLog: [new ChangeLogEntry(at, "Kai", summary)]);
            TaskItem toWrite = task with { Path = TaskLayout.PathFor(this.Harness.Store.RootDirectory, task.Location, task.Id) };
            _ = this.Harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite)) ?? throw new InvalidOperationException("Fixture collided with an existing file.");
        }

        /// <summary>Writes an empty report file for a date into the Team's <c>_retros</c> folder.</summary>
        /// <param name="fileName">The report file name.</param>
        public void WriteReport(string fileName)
        {
            string folder = Path.Combine(this.TeamFolder, TeamPaths.RetrosDir);
            _ = Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, fileName), "# Retro");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Harness.Dispose();
            this.libraryDir.Dispose();
        }
    }

    /// <summary>Builds the scene with the personas and one directory user per caller.</summary>
    /// <param name="ct">Cancels the setup.</param>
    private static async Task<Scene> CreateSceneAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string[] skill = ["retrospective"];
        harness.Personas.Add(new PersonaIdentity("Mia", "Lead", "mia", [Team], Skills: skill, Role: new RoleRef("web", "lead", "1.2.0")), "You are Mia.");
        harness.Personas.Add(new PersonaIdentity("Rae", "Retro", "rae", [Team], Skills: skill, Role: new RoleRef("web", "retro", "1.2.0")), "You are Rae.");
        harness.Personas.Add(new PersonaIdentity("Sam", "Dev", "sam", [Team], Skills: skill), "You are Sam.");
        harness.Personas.Add(new PersonaIdentity("Zed", "Dev", "zed", [Team], Role: new RoleRef("web", "retro", "1.2.0")), "You are Zed.");
        harness.Personas.Add(new PersonaIdentity("Ola", "Retro", "ola", ["Design"], Skills: skill, Role: new RoleRef("web", "retro", "1.2.0")), "You are Ola.");
        Scene scene = new() { Harness = harness, Chat = new FileChatStore(harness.Options, NullLogger<FileChatStore>.Instance) };
        foreach (string name in new[] { "Mia", "Rae", "Sam", "Zed", "Ola" })
        {
            User? user = await harness.Directory.UpsertAgentUserAsync(name, null, ct);
            scene.Ids[name] = user?.Id ?? throw new InvalidOperationException("Directory refused a user.");
        }

        return scene;
    }

    /// <summary>Builds the call arguments.</summary>
    private static JsonObject Args(string team = Team, string? since = null, string? room = null, string? taskId = null)
    {
        JsonObject arguments = new() { ["team"] = team };
        if (since is not null)
        {
            arguments["since"] = since;
        }

        if (room is not null)
        {
            arguments["room"] = room;
        }

        if (taskId is not null)
        {
            arguments["taskId"] = taskId;
        }

        return arguments;
    }

    /// <summary>With no room or Task, the Retro agent gets the Team's rendered bundle.</summary>
    [Fact]
    public async Task Invoke_NoRoomOrTask_ReturnsTheBundle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("PLAT-0001", Now.AddDays(-1), "status: Review → To Do");

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        string[] lines = result.Split('\n');
        Assert.Equal("Records for Platform since 2026-09-08", lines[0]);
        Assert.Contains("bounce 2026-10-07 12:00 Kai PLAT-0001 status: Review → To Do [PLAT-0001]", lines);
    }

    /// <summary>A Team with a register gets the "Rows to verify" section first, listing only its Done and Partial rows (Task 78).</summary>
    [Fact]
    public async Task Invoke_TeamWithRegister_LeadsWithRowsToVerify()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = Directory.CreateDirectory(scene.TeamFolder);
        RegisterRow[] rows =
        [
            new RegisterRow("R-001", "Slow reviews", "Checklist", "Mia", RetroStatus.Open, "later"),
            new RegisterRow("R-002", "Flaky tests", "Quarantine", "Sam", RetroStatus.Done, "watch"),
        ];
        File.WriteAllText(Path.Combine(scene.TeamFolder, TeamPaths.RetroRegisterFileName), RetroRegister.Render(rows));

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        string[] lines = result.Split('\n');
        Assert.Equal("Rows to verify:", lines[0]);
        Assert.Equal("R-002: Flaky tests | action: Quarantine | check: watch", lines[1]);
        Assert.Equal("Records for Platform since 2026-09-08", lines[2]);
    }

    /// <summary>A register that cannot be read (another process holds it) gives a normal refusal, not an exception.</summary>
    [Fact]
    public async Task Invoke_RegisterLocked_ReturnsRefusal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        _ = Directory.CreateDirectory(scene.TeamFolder);
        string path = Path.Combine(scene.TeamFolder, TeamPaths.RetroRegisterFileName);
        File.WriteAllText(path, RetroRegister.Render([]));
        using FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        Assert.Equal("The retro register could not be read right now; try again.", result);
    }

    /// <summary>The Team's manager may read too.</summary>
    [Fact]
    public async Task Invoke_Manager_ReturnsTheBundle()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args(), ct);

        Assert.Equal("Records for Platform since 2026-09-08", result.Split('\n')[0]);
    }

    /// <summary>An explicit <c>since</c> narrows the bundle to changes after it.</summary>
    [Fact]
    public async Task Invoke_ExplicitSince_LeavesOutOlderChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("PLAT-0001", Now.AddDays(-10), "status: Review → To Do");

        string result = await scene.Tool("Rae").InvokeAsync(Args(since: "2026-10-05"), ct);

        string[] lines = result.Split('\n');
        Assert.Equal("Records for Platform since 2026-10-05", lines[0]);
        Assert.Contains("review bounces: 0", lines);
    }

    /// <summary>With no <c>since</c>, the window starts at the last report date in <c>_retros</c>.</summary>
    [Fact]
    public async Task Invoke_NoSince_StartsAtTheLastReportDate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.WriteReport("2026-09-20.md");
        scene.WriteReport("2026-10-01.md");
        scene.WriteReport("2026-10-01-2.md");

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        Assert.Equal("Records for Platform since 2026-10-01", result.Split('\n')[0]);
    }

    /// <summary>Without a report, the window is the last 30 days.</summary>
    [Fact]
    public async Task Invoke_NoSinceNoReport_UsesThirtyDays()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        Assert.Equal("Records for Platform since 2026-09-08", result.Split('\n')[0]);
    }

    /// <summary>The <c>taskId</c> form returns the Task's Change log and Evidence.</summary>
    [Fact]
    public async Task Invoke_TaskId_ReturnsChangeLogAndEvidence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("PLAT-0001", Now.AddDays(-1), "status: Review → Done", "Body\n\n## Evidence\nChecked by hand");

        string result = await scene.Tool("Rae").InvokeAsync(Args(taskId: "PLAT-0001"), ct);

        Assert.Equal("PLAT-0001 T (Review)\nchange log:\n2026-10-07 12:00 Kai status: Review → Done\nevidence:\nChecked by hand", result);
    }

    /// <summary>A Task of another Team is not readable through this Team's call.</summary>
    [Fact]
    public async Task Invoke_TaskOfAnotherTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        scene.Seed("DES-0001", Now.AddDays(-1), "status: Review → Done", team: "Design");

        string result = await scene.Tool("Rae").InvokeAsync(Args(taskId: "DES-0001"), ct);

        Assert.Equal("Task DES-0001 is not in Team Platform.", result);
    }

    /// <summary>The <c>room</c> form returns the last 200 messages compactly, each cut to 300 characters, within the output cap.</summary>
    [Fact]
    public async Task Invoke_Room_ReturnsTheTailCompactAndCapped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        for (int i = 0; i < 250; i++)
        {
            string text = string.Create(CultureInfo.InvariantCulture, $"m{i:000} ") + new string('x', 400);
            await scene.Chat.AppendAsync(RoomId, new ChatMessage($"id{i}", Now.AddMinutes(i - 400), "u1", "Rae", text), ct);
        }

        string result = await scene.Tool("Rae").InvokeAsync(Args(room: RoomId), ct);

        Assert.True(result.Length <= 12000);
        Assert.DoesNotContain("m049", result, StringComparison.Ordinal);
        Assert.Equal("2026-10-08 09:29 Rae: m249 ", result.Split('\n')[^1][..27]);
        Assert.DoesNotContain(new string('x', 301), result, StringComparison.Ordinal);
    }

    /// <summary>A short transcript is returned whole, one line per message, in time order.</summary>
    [Fact]
    public async Task Invoke_ShortRoom_ReturnsOneLinePerMessage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("a", Now.AddHours(-2), "u1", "Rae", "first\nline two"), ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("b", Now.AddHours(-1), "u2", "Mia", "second"), ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args(room: RoomId), ct);

        Assert.Equal("2026-10-08 10:00 Rae: first line two\n2026-10-08 11:00 Mia: second", result);
    }

    /// <summary>A Room in which no member of the named Team has posted is refused, so a Team's call cannot read unrelated transcripts.</summary>
    [Fact]
    public async Task Invoke_RoomWithoutTeamMembers_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("a", Now.AddHours(-2), "u1", "Stranger", "hello"), ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(room: RoomId), ct);

        Assert.Equal("Room room-1 has no messages from Team Platform.", result);
    }

    /// <summary>Review fix 3: a Room that also holds a member of another Team is refused, even when a Platform member posted in it.</summary>
    [Fact]
    public async Task Invoke_CrossTeamRoom_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("a", Now.AddHours(-2), "u1", "Rae", "hello"), ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("b", Now.AddHours(-1), "u2", "Ola", "secret from Design"), ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(room: RoomId), ct);

        Assert.Equal("Room room-1 has participants outside Team Platform.", result);
    }

    /// <summary>Review fix 3: the Human taking part in a Team's Room does not make it a cross-Team Room.</summary>
    [Fact]
    public async Task Invoke_RoomWithTeamAndHuman_IsRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        string human = scene.Harness.Options.Value.HumanName;
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("a", Now.AddHours(-2), "u1", "Rae", "hello"), ct);
        await scene.Chat.AppendAsync(RoomId, new ChatMessage("b", Now.AddHours(-1), "u0", human, "thanks"), ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(room: RoomId), ct);

        Assert.Equal("2026-10-08 10:00 Rae: hello\n2026-10-08 11:00 " + human + ": thanks", result);
    }

    /// <summary>Review fix 6: a Room id the chat store rejects gives a normal refusal rather than an exception.</summary>
    [Fact]
    public async Task Invoke_InvalidRoomId_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(room: "../outside"), ct);

        Assert.Equal("Room '../outside' is not a valid Room id.", result);
    }

    /// <summary>A caller that is not a member of the Team is refused.</summary>
    [Fact]
    public async Task Invoke_NonMember_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Ola").InvokeAsync(Args(), ct);

        Assert.Equal("You are not a member of Team Platform.", result);
    }

    /// <summary>A member who is neither the manager nor the Retro agent is refused.</summary>
    [Fact]
    public async Task Invoke_MemberWithoutRetroRole_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Sam").InvokeAsync(Args(), ct);

        Assert.Equal("Only the Team's manager or its Retro agent may read its records.", result);
    }

    /// <summary>A Retro agent without the <c>retrospective</c> Skill is refused.</summary>
    [Fact]
    public async Task Invoke_WithoutTheSkill_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Zed").InvokeAsync(Args(), ct);

        Assert.Equal("You do not hold the retrospective Skill.", result);
    }

    /// <summary>A Team that does not exist is refused.</summary>
    [Fact]
    public async Task Invoke_UnknownTeam_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(team: "Nowhere"), ct);

        Assert.Equal("There is no Team named Nowhere.", result);
    }

    /// <summary>A missing team argument and an unknown Task id are refused with their messages.</summary>
    [Fact]
    public async Task Invoke_MissingTeamOrUnknownTask_AreRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string noTeam = await scene.Tool("Rae").InvokeAsync(Args(team: " "), ct);
        string noTask = await scene.Tool("Rae").InvokeAsync(Args(taskId: "PLAT-9999"), ct);

        Assert.Equal("team is required.", noTeam);
        Assert.Equal("There is no Task PLAT-9999.", noTask);
    }

    /// <summary>The tool's name and description are what the Retro agent is told.</summary>
    [Fact]
    public async Task Description_NamesTheThreeForms()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        ReadRecordsTool tool = scene.Tool("Rae");

        Assert.Equal("read_records", tool.Name);
        Assert.Equal(
            "Reads a Team's evidence for a retro. With only 'team' (and optionally 'since', a date like 2026-10-01) it returns the " +
            "compact records bundle. With 'taskId' it returns that Task's Change log and Evidence. With 'room' it returns the last " +
            "200 messages of that Room's transcript, each cut to 300 characters. Output is capped at 12000 characters.",
            tool.Description);
    }

    /// <summary>A malformed <c>since</c> is refused.</summary>
    [Fact]
    public async Task Invoke_BadSince_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(since: "last week"), ct);

        Assert.Equal("since must be a date like 2026-10-01.", result);
    }

    /// <summary>The bundle output is cut at 12,000 characters however many events there are.</summary>
    [Fact]
    public async Task Invoke_ManyEvents_HoldsTheCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        for (int i = 0; i < 300; i++)
        {
            scene.Seed(string.Create(CultureInfo.InvariantCulture, $"PLAT-{i + 1:0000}"), Now.AddHours(-i), "status: Review → To Do " + new string('y', 80));
        }

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        Assert.True(result.Length <= 12000);
    }

    /// <summary>The tool-call log is found for a member whose name is not a valid folder name, under the writer's sanitised name.</summary>
    [Fact]
    public async Task Invoke_MemberWithUnsafeName_ReadsTheSanitisedLogFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        string folder = Path.Combine(scene.Harness.Options.Value.DataDir, TeamPaths.ToolCallLogDir, "Ke_vin");
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "2026-10-07.jsonl"), "{\"time\":\"2026-10-07T10:00:00+00:00\",\"tool\":\"bash\",\"status\":\"failed\"}\n", ct);

        string result = await scene.Tool("Rae").InvokeAsync(Args(), ct);

        Assert.Contains("tool calls: 1 (1 failed)", result.Split('\n'));
    }
}
