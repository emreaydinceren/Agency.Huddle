using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Retro verification (Task 78, RT-7): which register rows are due, and the mark_retro_row tool that closes them.</summary>
public sealed class RetroVerifierTests
{
    private const string Team = "Platform";

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

    private static readonly DateTimeOffset LastRetro = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static RegisterRow[] FiveRows() =>
    [
        new RegisterRow("R-001", "Slow reviews", "Add checklist", "Mia", RetroStatus.Open, "check next retro"),
        new RegisterRow("R-002", "Flaky tests", "Quarantine", "Sam", RetroStatus.Done, "watch the next sprint"),
        new RegisterRow("R-003", "Late specs", "Spec first", "Mia", RetroStatus.Partial, "one spec still late"),
        new RegisterRow("R-004", "Big diffs", "Split work", "Sam", RetroStatus.Verified, "PLAT-0007"),
        new RegisterRow("R-005", "No QA", "Add QA", "Mia", RetroStatus.Changed, "replaced by R-006"),
    ];

    /// <summary>A Platform scene: Mia manages and holds the Skill, Sam holds it without a role.</summary>
    private sealed class Scene : IDisposable
    {
        private readonly TempDataDir libraryDir = new();

        /// <summary>The Task tool harness.</summary>
        public required TaskToolHarness Harness { get; init; }

        /// <summary>The caller ids by name.</summary>
        public Dictionary<string, string> Ids { get; } = new(StringComparer.Ordinal);

        /// <summary>The Team folder.</summary>
        public string TeamFolder => Path.Combine(this.libraryDir.Path, Team);

        /// <summary>The Team's register file.</summary>
        public string RegisterPath => Path.Combine(this.TeamFolder, TeamPaths.RetroRegisterFileName);

        /// <summary>Builds the tool for a caller over a register holding <see cref="FiveRows"/>.</summary>
        /// <param name="caller">The caller's name.</param>
        /// <param name="prompts">The prompt source the tool reads its description from; the seeded default when omitted.</param>
        public MarkRetroRowTool Tool(string caller, IPromptSource? prompts = null)
        {
            _ = Directory.CreateDirectory(this.TeamFolder);
            if (!File.Exists(this.RegisterPath))
            {
                File.WriteAllText(this.RegisterPath, "# Retro register\n\n" + RetroRegister.Render(FiveRows()));
            }

            FakeTeamCatalog catalog = new()
            {
                Teams = [new TeamSummary(Team, [], ["Mia", "Sam", "Zed"], true) { TeamType = new VersionedRef("web", "1.2.0"), Manager = "Mia" }],
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
            return new MarkRetroRowTool(catalog, this.Harness.Personas, types, new RetroFileStore(folders), this.Harness.Directory, this.Ids[caller], prompts ?? new FakePromptSource());
        }

        /// <summary>Reads the status of a register row.</summary>
        /// <param name="rowId">The row id.</param>
        public RetroStatus StatusOf(string rowId) => RetroRegister.Parse(File.ReadAllText(this.RegisterPath)).Single(row => row.Id == rowId).Status;

        /// <inheritdoc />
        public void Dispose()
        {
            this.Harness.Dispose();
            this.libraryDir.Dispose();
        }
    }

    private static async Task<Scene> CreateSceneAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        string[] skill = ["retrospective"];
        harness.Personas.Add(new PersonaIdentity("Mia", "Lead", "mia", [Team], Skills: skill, Role: new RoleRef("web", "lead", "1.2.0")), "You are Mia.");
        harness.Personas.Add(new PersonaIdentity("Sam", "Dev", "sam", [Team], Skills: skill), "You are Sam.");
        harness.Personas.Add(new PersonaIdentity("Zed", "Dev", "zed", [Team]), "You are Zed.");
        Scene scene = new() { Harness = harness };
        foreach (string name in new[] { "Mia", "Sam", "Zed" })
        {
            User? user = await harness.Directory.UpsertAgentUserAsync(name, null, ct);
            scene.Ids[name] = user?.Id ?? throw new InvalidOperationException("Directory refused a user.");
        }

        return scene;
    }

    /// <summary>
    /// D5: an override saved for <c>tool.markRetroRow.description</c> is the text the tool's Description returns, read from the
    /// catalog through the store.
    /// </summary>
    [Fact]
    public async Task MarkRetroRowDescription_OverriddenValue_IsWhatTheToolReturns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var dataDir = new TempDataDir();
        var store = new PromptStore(dataDir.Options(), NullLogger<PromptStore>.Instance);
        const string overriddenText = "Marks a retro row as Verified, Partial or Changed.";

        store.Save("tool.markRetroRow.description", overriddenText);
        using Scene scene = await CreateSceneAsync(ct);

        Assert.Equal(overriddenText, scene.Tool("Mia", store).Description);
    }

    private static JsonObject Args(string rowId, string status, string evidence) =>
        new() { ["team"] = Team, ["rowId"] = rowId, ["status"] = status, ["evidence"] = evidence };

    /// <summary>Only Done and Partial rows are due; Open rows wait, Verified and Changed are final.</summary>
    [Fact]
    public void Due_MixedRows_ReturnsDoneAndPartialInFileOrder()
    {
        IReadOnlyList<VerificationPrompt> due = RetroVerifier.Due(FiveRows());

        Assert.Equal(
            [
                new VerificationPrompt("R-002", "Flaky tests", "Quarantine", "watch the next sprint"),
                new VerificationPrompt("R-003", "Late specs", "Spec first", "one spec still late"),
            ],
            due);
    }

    /// <summary>A Task id, a log line reference and a retro file each count as a link; plain prose does not.</summary>
    [Theory]
    [InlineData("PLAT-0042 shows no bounce since", true)]
    [InlineData("see logs/Sam/2026-10-09.jsonl#L12", true)]
    [InlineData("_retros/2026-10-15.md found it fixed", true)]
    [InlineData("it seems better now", false)]
    [InlineData("", false)]
    public void HasEvidenceLink_Text_RecognisesLinks(string evidence, bool expected)
    {
        Assert.Equal(expected, RetroVerifier.HasEvidenceLink(evidence));
    }

    /// <summary>Verified without a link is refused and the register keeps the row Done.</summary>
    [Fact]
    public async Task Invoke_VerifiedWithoutLink_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args("R-002", "Verified", "it feels better"), ct);

        Assert.Equal("Verified needs evidence from after the change.", result);
        Assert.Equal(RetroStatus.Done, scene.StatusOf("R-002"));
    }

    /// <summary>Verified with a Task id link is accepted and the register stores the evidence.</summary>
    [Fact]
    public async Task Invoke_VerifiedWithLink_SetsStatusAndEvidence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args("R-002", "Verified", "PLAT-0042 passed first time"), ct);

        Assert.Equal("Row R-002 is now Verified.", result);
        RegisterRow row = RetroRegister.Parse(File.ReadAllText(scene.RegisterPath)).Single(r => r.Id == "R-002");
        Assert.Equal(RetroStatus.Verified, row.Status);
        Assert.Equal("PLAT-0042 passed first time", row.EvidenceNextCheck);
    }

    /// <summary>An Open row cannot be marked Verified, even with a link.</summary>
    [Fact]
    public async Task Invoke_OpenRow_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args("R-001", "Verified", "PLAT-0042 passed"), ct);

        Assert.Equal("Row R-001 is not due: only a Done or Partial row can be checked.", result);
        Assert.Equal(RetroStatus.Open, scene.StatusOf("R-001"));
    }

    /// <summary>Partial needs no link, only some evidence text.</summary>
    [Fact]
    public async Task Invoke_PartialWithoutLink_IsAccepted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string result = await scene.Tool("Mia").InvokeAsync(Args("R-002", "Partial", "helped less than planned"), ct);

        Assert.Equal("Row R-002 is now Partial.", result);
        Assert.Equal(RetroStatus.Partial, scene.StatusOf("R-002"));
    }

    /// <summary>Empty evidence, an unknown status and an unknown row are each refused with their own message.</summary>
    [Fact]
    public async Task Invoke_BadArguments_AreRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);
        MarkRetroRowTool tool = scene.Tool("Mia");

        Assert.Equal("evidence is required.", await tool.InvokeAsync(Args("R-002", "Partial", " "), ct));
        Assert.Equal("status must be Verified, Partial or Changed.", await tool.InvokeAsync(Args("R-002", "Done", "PLAT-0042"), ct));
        Assert.Equal("The register has no row R-099.", await tool.InvokeAsync(Args("R-099", "Partial", "some"), ct));
    }

    /// <summary>A member who is neither the manager nor the Retro agent is refused, as is one without the Skill.</summary>
    [Fact]
    public async Task Invoke_WrongCaller_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        string specialist = await scene.Tool("Sam").InvokeAsync(Args("R-002", "Partial", "some"), ct);
        string noSkill = await scene.Tool("Zed").InvokeAsync(Args("R-002", "Partial", "some"), ct);

        Assert.Equal("Only the Team's manager or its Retro agent may mark its register rows.", specialist);
        Assert.Equal("You do not hold the retrospective Skill.", noSkill);
        Assert.Equal(RetroStatus.Done, scene.StatusOf("R-002"));
    }

    /// <summary>The tool is named mark_retro_row and its description says what it is for.</summary>
    [Fact]
    public async Task Tool_NameAndDescription_ArePinned()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Scene scene = await CreateSceneAsync(ct);

        MarkRetroRowTool tool = scene.Tool("Mia");

        Assert.Equal("mark_retro_row", tool.Name);
        Assert.Equal(
            "Marks a Done or Partial row of the Team's Retro register as Verified, Partial or Changed after checking it against later evidence. " +
            "Verified needs evidence that links a Task id, a logs/... line (logs/...#L12) or a _retros/ file.",
            tool.Description);
    }

    /// <summary>The records text leads with a "Rows to verify" section listing the due rows.</summary>
    [Fact]
    public void RenderText_WithDueRows_LeadsWithTheSection()
    {
        RecordsBundle bundle = new(Team, LastRetro, [], [], [], [], [], new Dictionary<string, IReadOnlyList<StoredSpend>>(StringComparer.Ordinal), [], []);

        string[] lines = RecordsBundleBuilder.RenderText(bundle, 12000, RetroVerifier.Due(FiveRows())).Split('\n');

        Assert.Equal("Rows to verify:", lines[0]);
        Assert.Equal("R-002: Flaky tests | action: Quarantine | check: watch the next sprint", lines[1]);
        Assert.Equal("R-003: Late specs | action: Spec first | check: one spec still late", lines[2]);
        Assert.Equal("Records for Platform since 2026-10-01", lines[3]);
    }

    /// <summary>With a register but nothing due the section says none; with no register the text has no section.</summary>
    [Fact]
    public void RenderText_NothingDueOrNoRegister_ShowsNoneOrNothing()
    {
        RecordsBundle bundle = new(Team, LastRetro, [], [], [], [], [], new Dictionary<string, IReadOnlyList<StoredSpend>>(StringComparer.Ordinal), [], []);

        string[] none = RecordsBundleBuilder.RenderText(bundle, 12000, []).Split('\n');
        string[] absent = RecordsBundleBuilder.RenderText(bundle).Split('\n');

        Assert.Equal("Rows to verify: none", none[0]);
        Assert.Equal("Records for Platform since 2026-10-01", none[1]);
        Assert.Equal("Records for Platform since 2026-10-01", absent[0]);
    }
}
