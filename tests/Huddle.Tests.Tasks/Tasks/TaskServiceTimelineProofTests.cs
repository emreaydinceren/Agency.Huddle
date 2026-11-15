using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Tasks;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofContextBuilder = Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder;
using ProofSource = Agency.Huddle.App.Tasks.HumanGate.ProofSource;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// The done-when of #586 at the service level: on a gated Task whose Proof needs a verifying Agent's Evidence and a command that
/// exits 0, no description write (an Agent's <c>update_task</c>, or the Human's Save from a stale form) removes any Evidence
/// or moves the Proof, whether the file is in the timeline format or an older one that the first write converts.
/// </summary>
public sealed class TaskServiceTimelineProofTests
{
    private const string TeamFileText = """
        ---
        teamType: 'web@1.2.0'
        gates: ['qa-evidence-before-done']
        checkCommand: 'dotnet test'
        ---
        """;

    private const string Process = """
        # Flow

        ## Definition of Done

        - Tests pass: command `dotnet test` exits 0
        """;

    private const string LegacyFile =
        "---\nid: PLAT-0001\ntitle: Fix login\nstatus: In Progress\npriority: Medium\ncreator: You\nhuman_gate: true\n---\n"
        + "Plan the thing.\n\n## Evidence\n"
        + "- 2026-10-08T12:00Z | Lead | dotnet build | exit 0\n"
        + "Checked by hand on staging.\n"
        + "- 2026-10-08T12:30Z | Quinn | dotnet test | exit 0\n"
        + "\n## Change log\n"
        + "- 2026-10-08T11:00:00Z | You | created\n"
        + "- 2026-10-08T12:00:05Z | Lead | evidence added by Lead\n"
        + "- 2026-10-08T12:30:04Z | Quinn | evidence added by Quinn\n";

    private static readonly DateTimeOffset Later = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TaskActor Lead = new(TaskActorKind.Agent, "Lead", "lead-id");
    private static readonly TaskActor Quinn = new(TaskActorKind.Agent, "Quinn", "quinn-id");

    /// <summary>What a Task's Evidence and Proof look like at one moment.</summary>
    /// <param name="Evidence">Each Evidence line's time, author, command, exit code and text, one per line.</param>
    /// <param name="Met">How many Proof items are met.</param>
    /// <param name="Total">How many Proof items there are.</param>
    private sealed record Snapshot(string Evidence, int Met, int Total);

    /// <summary>Evidence from an Agent through <c>AddEvidence</c>, then a description write by an Agent and one by the Human from a stale form: nothing is lost and the Proof does not move.</summary>
    [Fact]
    public void Update_DescriptionAfterEvidence_KeepsEvidenceAndProof()
    {
        using Rig rig = new(legacyFile: null);
        TaskId id = rig.CreateGated("Plan the thing.");
        Assert.IsType<TaskResult.Saved>(rig.Service.AddEvidence(id, Run("dotnet build"), Lead));
        string loadedByTheForm = rig.Get(id).Version;
        Assert.IsType<TaskResult.Saved>(rig.Service.AddEvidence(id, Run("dotnet test"), Quinn));
        Snapshot before = rig.Snapshot(id);
        Assert.Equal(2, before.Total);
        Assert.Equal(before.Total, before.Met);
        Assert.NotEqual(loadedByTheForm, rig.Get(id).Version);

        TaskResult humanSave = rig.Service.Update(id, new TaskPatch { Description = "Edited by the Human." }, loadedByTheForm, rig.Human);

        Assert.IsType<TaskResult.Saved>(humanSave);
        Assert.Equal("Edited by the Human.", rig.Get(id).Description);
        Assert.Equal(before, rig.Snapshot(id));

        TaskResult agentUpdate = rig.Service.Update(id, new TaskPatch { Description = "Rewritten by the manager." }, null, Lead);

        Assert.IsType<TaskResult.Saved>(agentUpdate);
        Assert.Equal("Rewritten by the manager.", rig.Get(id).Description);
        Assert.Equal(before, rig.Snapshot(id));
        Assert.DoesNotContain("## Evidence", rig.Text(id), StringComparison.Ordinal);
    }

    /// <summary>A file written before the timeline keeps its Evidence and Proof through the description write that converts it.</summary>
    [Fact]
    public void Update_DescriptionOnAnOlderFile_KeepsEvidenceAndProofAndConvertsTheFile()
    {
        using Rig rig = new(LegacyFile);
        TaskId id = new("PLAT", 1);
        Snapshot before = rig.Snapshot(id);
        Assert.Equal(2, before.Total);
        Assert.Equal(before.Total, before.Met);
        Assert.Contains("## Evidence", rig.Text(id), StringComparison.Ordinal);

        TaskResult result = rig.Service.Update(id, new TaskPatch { Description = "Rewritten by the manager." }, null, Lead);

        Assert.IsType<TaskResult.Saved>(result);
        string text = rig.Text(id);
        Assert.Contains("## Timeline", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Evidence", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Change log", text, StringComparison.Ordinal);
        Assert.Equal("Rewritten by the manager.", rig.Get(id).Description);
        Assert.Equal(before, rig.Snapshot(id));
    }

    /// <summary>The Human's Save of a description on an older file also keeps the Evidence, and a second write after the conversion changes nothing about it.</summary>
    [Fact]
    public void Update_HumanDescriptionOnAnOlderFile_KeepsEvidenceThroughTwoWrites()
    {
        using Rig rig = new(LegacyFile);
        TaskId id = new("PLAT", 1);
        Snapshot before = rig.Snapshot(id);
        Assert.Equal(before.Total, before.Met);
        string loaded = rig.Get(id).Version;

        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(id, new TaskPatch { Description = "First." }, loaded, rig.Human));
        Assert.Equal(before, rig.Snapshot(id));
        _ = Assert.IsType<TaskResult.Saved>(rig.Service.Update(id, new TaskPatch { Description = "Second." }, null, rig.Human));

        Assert.Equal(before, rig.Snapshot(id));
        Assert.Equal("Second.", rig.Get(id).Description);
    }

    /// <summary>A structured Evidence line for a command that exited 0.</summary>
    private static EvidenceLine Run(string command) => new(Later, "ignored", command, 0, null, null, string.Empty);

    /// <summary>A real Task stack, Proof builder and Team Process over one data folder, with Agents Lead and Quinn (who verifies).</summary>
    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        private readonly PersonaStore personas;
        private readonly TaskStore store;
        private readonly ProofContextBuilder builder;

        /// <summary>Builds the stack; an older-format file, when given, is on disk before the store first reads it.</summary>
        /// <param name="legacyFile">The text of <c>PLAT-0001.md</c> to start with, or <see langword="null"/>.</param>
        public Rig(string? legacyFile)
        {
            if (legacyFile is not null)
            {
                string path = Path.Combine(TestTaskStore.Root(this.dir), "Platform", "_tasks", "PLAT-0001.md");
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("No folder."));
                File.WriteAllText(path, legacyFile);
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 10, 8, 12, 30, 4, DateTimeKind.Utc));
            }

            this.personas = this.dir.CreatePersonaStore();
            _ = this.personas.Add(new PersonaIdentity("Lead", "Lead", "lead", ["Platform"], Role: new RoleRef("web", "lead", "1.2.0")), "You lead.");
            _ = this.personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", ["Platform"], Role: new RoleRef("web", "qa", "1.2.0")), "You verify.");
            this.store = TestTaskStore.CreateTaskStore(this.dir, this.personas);
            this.Service = new TaskService(
                this.store,
                new TaskIdAllocator(this.dir.Options()),
                new TaskEvents(),
                this.personas,
                new EmptySprintCatalog(),
                this.dir.Options(),
                new ManualTimeProvider(Later),
                NullLogger<TaskService>.Instance);
            File.WriteAllText(Path.Combine(this.dir.Path, "AGENTS.md"), Process);
            ProcessGateService gates = TaskGateText.BuildGates(this.dir, TeamFileText);
            FakeTeamCatalog catalog = new() { Teams = [new TeamSummary("Platform", [], ["Lead", "Quinn"], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
            DocHistoryStore history = new(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
            this.builder = new ProofContextBuilder(this.store, this.personas, gates, catalog, this.library.Resolver, history);
        }

        /// <summary>The service under test.</summary>
        public TaskService Service { get; }

        /// <summary>The Human's actor.</summary>
        public TaskActor Human => TaskActors.Human(this.dir.Options().Value);

        /// <summary>Creates a Human-gated Task in progress as the Human and returns its id.</summary>
        /// <param name="description">The description.</param>
        public TaskId CreateGated(string description)
        {
            TaskDraft draft = new("Fix login", "Platform", null, TaskState.InProgress, Description: description) { HumanGate = true };
            return Assert.IsType<TaskResult.Saved>(this.Service.Create(draft, this.Human)).Task.Id;
        }

        /// <summary>The stored Task.</summary>
        /// <param name="id">The Task id.</param>
        public TaskItem Get(TaskId id) => this.store.Get(id) ?? throw new InvalidOperationException("The Task is missing.");

        /// <summary>The Task's file text.</summary>
        /// <param name="id">The Task id.</param>
        public string Text(TaskId id) => this.store.ReadText(id) ?? throw new InvalidOperationException("The Task file is missing.");

        /// <summary>The Task's Evidence and Proof as the application computes them.</summary>
        /// <param name="id">The Task id.</param>
        public Snapshot Snapshot(TaskId id)
        {
            TaskItem task = this.Get(id);
            Proof proof = new ProofSource(this.builder).For(task);
            string evidence = string.Join('\n', task.Evidence.Select(static line => string.Create(CultureInfo.InvariantCulture, $"{line.Time:O}|{line.Author}|{line.Command}|{line.ExitCode}|{line.RawText}")));
            return new Snapshot(evidence, proof.MetCount, proof.Total);
        }

        /// <summary>Disposes the stores and folders.</summary>
        public void Dispose()
        {
            this.store.Dispose();
            this.personas.Dispose();
            this.Service.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
