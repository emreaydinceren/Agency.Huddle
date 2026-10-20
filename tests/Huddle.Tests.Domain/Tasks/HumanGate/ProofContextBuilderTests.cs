using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Proof = Agency.Huddle.App.Tasks.HumanGate.Proof;
using ProofContext = Agency.Huddle.App.Tasks.HumanGate.ProofContext;
using ProofContextBuilder = Agency.Huddle.App.Tasks.HumanGate.ProofContextBuilder;
using ProofDocument = Agency.Huddle.App.Tasks.HumanGate.ProofDocument;
using ProofSource =Agency.Huddle.App.Tasks.HumanGate.ProofSource;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>
/// <c>ProofContextBuilder.Build</c> gathers everything the evaluator needs from the live stores: the Team's Process, the
/// Task's Evidence, role coverage, the roster, the children and the history of each attached Markdown document.
/// </summary>
public sealed class ProofContextBuilderTests
{
    private const string Process = """
        ---
        title: 'Flow'
        gates: ['qa-evidence-before-done']
        checkCommand: 'dotnet test'
        ---
        # Flow

        ## Definition of Done

        - Tests pass: command `dotnet test` exits 0
        - Plan reviewed: document reviewed by verify
        """;

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>One end-to-end case over real stores: children, role coverage, Evidence and the attached document's history all arrive in the context.</summary>
    [Fact]
    public void Builder_ReadsChildrenCoverageAndAttachedHistory()
    {
        using Rig rig = new();
        TaskItem parent = rig.Parent(rig.Attach("plan.md"));
        TaskItem child = rig.Child(parent, "- [x] a\n- [ ] b");
        rig.Log("plan.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed));

        ProofContext context = rig.Builder.Build(parent);

        Assert.Equal(["Tests pass", "Plan reviewed"], context.Definition.Select(static item => item.Label));
        Assert.Equal([GateId.QaEvidenceBeforeDone], context.Gates);
        Assert.False(context.IsBug);
        Assert.Equal("Quinn", Assert.Single(context.Evidence).Author);
        Assert.Equal("verify", context.Coverage["Quinn"]);
        Assert.Equal(["Lead", "Quinn"], context.Roster);
        Assert.Equal(child.Id, Assert.Single(context.Children).Id);
        ProofDocument document = Assert.Single(context.Documents);
        Assert.Equal("plan.md", document.File);
        Assert.Equal([DocChangeKind.Created, DocChangeKind.Reviewed], document.History.Select(static entry => entry.Kind));
    }

    /// <summary>A path line that is not a Markdown file is not an attached document.</summary>
    [Fact]
    public void Build_NonMarkdownAttachment_IsIgnored()
    {
        using Rig rig = new();
        TaskItem parent = rig.Parent(rig.Attach("notes.txt"));

        ProofContext context = rig.Builder.Build(parent);

        Assert.Empty(context.Documents);
    }

    /// <summary>A Task tagged <c>bug</c> is flagged as a bug in its context.</summary>
    [Fact]
    public void Build_BugTag_SetsIsBug()
    {
        using Rig rig = new();
        TaskItem parent = rig.Parent(string.Empty) with { Tags = ["bug"] };

        ProofContext context = rig.Builder.Build(parent);

        Assert.True(context.IsBug);
    }

    /// <summary>Only a Task's own children count: a Task of another parent is not in the context.</summary>
    [Fact]
    public void Build_OtherTasksChild_IsNotIncluded()
    {
        using Rig rig = new();
        TaskItem parent = rig.Parent(string.Empty);
        TaskItem other = rig.Parent(string.Empty);
        _ = rig.Child(other, "- [ ] a");

        ProofContext context = rig.Builder.Build(parent);

        Assert.Empty(context.Children);
    }

    /// <summary>A Task of a Team with no Process has an empty Definition and no gates.</summary>
    [Fact]
    public void Build_TeamWithoutProcess_HasNoDefinitionAndNoGates()
    {
        using Rig rig = new();
        TaskItem task = TestTasks.Make(id: "NOPE-0001", location: new TaskLocation("Nowhere", null, false));

        ProofContext context = rig.Builder.Build(task);

        Assert.Empty(context.Definition);
        Assert.Empty(context.Gates);
    }

    /// <summary>The coverage map holds each named Teammate's role coverage and leaves out names with no Persona.</summary>
    [Fact]
    public void CoverageFor_NamesWithRoles_MapsThemAndSkipsUnknown()
    {
        using Rig rig = new();

        IReadOnlyDictionary<string, string> covers = rig.Builder.CoverageFor(["Quinn", "Ghost"]);

        Assert.Equal("verify", covers["Quinn"]);
        Assert.DoesNotContain("Ghost", covers.Keys);
    }

    /// <summary>The Team's Process is reachable through the gate service so the context sees its Definition of Done.</summary>
    [Fact]
    public void ProcessOf_TeamWithDefinition_ReturnsItsDocument()
    {
        using Rig rig = new();

        ProcessDocument? document = rig.Gates.ProcessOf(Rig.Team);

        Assert.Equal(2, document?.DefinitionOfDone.Count);
    }

    /// <summary>The proof source builds the context then evaluates it, so a Task with every rule satisfied is complete.</summary>
    [Fact]
    public void ProofSource_For_BuildsThenEvaluates()
    {
        using Rig rig = new();
        TaskItem parent = rig.Parent(rig.Attach("plan.md"));
        rig.Log("plan.md", Entry(0, "Lead", DocChangeKind.Created), Entry(1, "Quinn", DocChangeKind.Reviewed));
        ProofSource source = new(rig.Builder);

        Proof proof = source.For(parent);

        Assert.Equal(3, proof.Total);
        Assert.True(proof.IsComplete);
    }

    /// <summary>One history entry by an Agent.</summary>
    /// <param name="minutes">Minutes after noon.</param>
    /// <param name="actor">Who made it.</param>
    /// <param name="kind">What kind of change.</param>
    private static DocHistoryEntry Entry(int minutes, string actor, DocChangeKind kind) =>
        new(Noon.AddMinutes(minutes), actor, DocActorKind.Agent, kind, "s");

    /// <summary>Real stores: a Task harness with two role-bearing Teammates, a Library root, a document history store and a Team whose Process holds a Definition of Done.</summary>
    private sealed class Rig : IDisposable
    {
        /// <summary>The Team every Task belongs to.</summary>
        public const string Team = TaskGateText.Team;

        private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

        private readonly TempDataDir dir = new();
        private readonly TaskToolHarness harness = new();
        private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        private readonly string docsRoot;
        private readonly DocHistoryStore history;

        /// <summary>Seeds the Teammates, the Process and the stores.</summary>
        public Rig()
        {
            RoleRef lead = new("web", "lead", "1.2.0");
            RoleRef qa = new("web", "qa", "1.2.0");
            _ = this.harness.Personas.Add(new PersonaIdentity("Lead", "Lead", "lead", [Team], Role: lead), "You lead.");
            _ = this.harness.Personas.Add(new PersonaIdentity("Quinn", "QA Engineer", "quinn", [Team], Role: qa), "You verify.");
            this.docsRoot = this.library.CreatePinnedRoot("Docs");
            this.history = new DocHistoryStore(this.library.BoundOptions, NullLogger<DocHistoryStore>.Instance);
            this.Gates = TaskGateText.BuildGates(this.dir, Process);
            FakeTeamCatalog catalog = new() { Teams = [new TeamSummary(Team, [], ["Lead", "Quinn"], true) { TeamType = new VersionedRef("web", "1.2.0") }] };
            this.Builder = new ProofContextBuilder(this.harness.Store, this.harness.Personas, this.Gates, catalog, this.library.Resolver, this.history);
        }

        /// <summary>The gate service holding the Team's Process.</summary>
        public ProcessGateService Gates { get; }

        /// <summary>The builder under test.</summary>
        public ProofContextBuilder Builder { get; }

        /// <summary>Writes a file in the Library root and returns the description line that attaches it.</summary>
        /// <param name="fileName">The file name.</param>
        public string Attach(string fileName)
        {
            string path = Path.Combine(this.docsRoot, fileName);
            File.WriteAllText(path, "# Plan");
            return $"`{path}`";
        }

        /// <summary>Creates a parent Task whose description holds <paramref name="attachment"/> and one Evidence line by Quinn.</summary>
        /// <param name="attachment">A path line, or empty.</param>
        public TaskItem Parent(string attachment)
        {
            TaskResult result = this.harness.Service.Create(new TaskDraft("Parent", Team, null), Nova);
            TaskItem saved = Assert.IsType<TaskResult.Saved>(result).Task;
            EvidenceLine line = new(Noon, "Quinn", "dotnet test", 0, null, null, "raw");
            return saved with { Description = $"{attachment}\n\n## Evidence\n\n{line.Format()}\n" };
        }

        /// <summary>Creates a child Task of <paramref name="parent"/> in the store.</summary>
        /// <param name="parent">The parent.</param>
        /// <param name="description">The child's description.</param>
        public TaskItem Child(TaskItem parent, string description)
        {
            TaskResult result = this.harness.Service.Create(new TaskDraft("Child", Team, null, Parent: parent.Id, Tags: ["citation-check"], Description: description), Nova);
            return Assert.IsType<TaskResult.Saved>(result).Task;
        }

        /// <summary>Appends <paramref name="entries"/> to the history of a file in the Library root.</summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="entries">The entries, oldest first.</param>
        public void Log(string fileName, params DocHistoryEntry[] entries)
        {
            foreach (DocHistoryEntry entry in entries)
            {
                this.history.Append(this.library.Resolve(this.docsRoot, fileName), entry);
            }
        }

        /// <summary>Disposes the stores and temp folders.</summary>
        public void Dispose()
        {
            this.harness.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}
