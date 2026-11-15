namespace Agency.Huddle.Tests.Acp;

using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pins <see cref="NeedsYouLevelGuard"/> (ADR-0088): an Agent cannot set the Needs You level or its spend cap. An edit that names either key, or whose
/// result would change one, a write that carries a different level or cap than the file on disk, and a delete or move of a file that carries one are
/// refused; a read and every other edit of those files are allowed.
/// </summary>
public sealed class NeedsYouLevelGuardTests : IDisposable
{
    private const string Refusal = "Only the Human sets the Needs You level and its spend cap, in the app. Do not edit the needsYouLevel or spendCap lines.";

    private const string TeamWithLevel = "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5'\n---\nNotes about the Team.\n";

    private const string TeamWithoutLevel = "---\nteamType: 'development@1.0.0'\n---\nNotes about the Team.\n";

    private const string ProjectWithLevel = "---\nneedsYouLevel: 'ask-me'\n---\nProject notes.\n";

    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "huddle-needsyou-" + Guid.NewGuid().ToString("N"));

    private readonly string teamsRoot;

    private readonly string workDir;

    private readonly NeedsYouLevelGuard guard;

    /// <summary>Creates the Teams root with the Team <c>Home</c> (a level in its file) and <c>Plain</c> (none), and the Project <c>Repairs</c> (a level).</summary>
    public NeedsYouLevelGuardTests()
    {
        this.teamsRoot = Path.Combine(this.sandbox, "Teams");
        this.workDir = Path.Combine(this.sandbox, "Agents", "Nova", "work");
        foreach (string folder in new[] { "Home/Repairs", "Home/memory", "Plain" })
        {
            _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, folder.Replace('/', Path.DirectorySeparatorChar)));
        }

        _ = Directory.CreateDirectory(this.workDir);
        File.WriteAllText(this.InTeams("Home/_team.md"), TeamWithLevel);
        File.WriteAllText(this.InTeams("Home/Repairs/_project.md"), ProjectWithLevel);
        File.WriteAllText(this.InTeams("Plain/_team.md"), TeamWithoutLevel);
        this.guard = new NeedsYouLevelGuard(this.teamsRoot, this.workDir, new FakePromptSource());
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose() => Directory.Delete(this.sandbox, recursive: true);

    /// <summary>An edit whose old or new text names either key is refused, in a Team file and in a Project file, whatever the case of the key.</summary>
    /// <param name="relative">The file below the Teams root.</param>
    /// <param name="oldText">The text to replace.</param>
    /// <param name="newText">The replacement.</param>
    [Theory]
    [InlineData("Home/_team.md", "needsYouLevel: 'run-inside-team'", "needsYouLevel: 'ask-me'")]
    [InlineData("Home/_team.md", "Notes about the Team.", "spendCap: '500'")]
    [InlineData("Plain/_team.md", "teamType", "NEEDSYOULEVEL: 'run-inside-team'\nteamType")]
    [InlineData("Home/Repairs/_project.md", "needsYouLevel: 'ask-me'", "needsYouLevel: 'run-inside-team'")]
    [InlineData("Home/Repairs/_project.md", "Project notes.", "SPENDCAP: '9'")]
    public void Edit_NamingAKey_IsRefused(string relative, string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Equal(Refusal, this.guard.RefusalFor(Edit(this.InTeams(relative), oldText, newText)));
    }

    /// <summary>An edit that names no key but would change the level by replacing only its value is refused, because the result is judged and not only the words.</summary>
    [Fact]
    public void Edit_ReplacingOnlyTheValue_IsRefused()
    {
        ToolCallInfo call = Edit(this.InTeams("Home/_team.md"), "'run-inside-team'", "'ask-me'");

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>A multi-edit is read edit by edit: one of its edits that sets the level refuses the whole call.</summary>
    [Fact]
    public void MultiEdit_OneEditChangesTheLevel_IsRefused()
    {
        string json = JsonSerializer.Serialize(new
        {
            file_path = this.InTeams("Plain/_team.md"),
            edits = new object[]
            {
                new { old_string = "Notes about the Team.", new_string = "Other notes." },
                new { old_string = "teamType: 'development@1.0.0'", new_string = "teamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'" },
            },
        });

        Assert.Equal(Refusal, this.guard.RefusalFor(new ToolCallInfo("call-1", "Tool", ToolKind.Edit, ToolCallStatus.Pending, json)));
    }

    /// <summary>An edit of another line of the same files, even one beside the level, is allowed.</summary>
    /// <param name="relative">The file below the Teams root.</param>
    /// <param name="oldText">The text to replace.</param>
    /// <param name="newText">The replacement.</param>
    [Theory]
    [InlineData("Home/_team.md", "Notes about the Team.", "Better notes.")]
    [InlineData("Home/_team.md", "teamType: 'development@1.0.0'", "teamType: 'development@1.1.0'")]
    [InlineData("Home/Repairs/_project.md", "Project notes.", "More notes.")]
    [InlineData("Plain/_team.md", "Notes about the Team.", "x")]
    public void Edit_OfAnotherLine_IsAllowed(string relative, string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Edit(this.InTeams(relative), oldText, newText)));
    }

    /// <summary>A whole-file write that carries a different level or cap than the file on disk is refused: a new level, a new cap, a dropped level, or a level in a file that has none.</summary>
    /// <param name="relative">The file below the Teams root.</param>
    /// <param name="content">The new whole content.</param>
    [Theory]
    [InlineData("Home/_team.md", "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'ask-me'\nspendCap: '5'\n---\nNotes about the Team.\n")]
    [InlineData("Home/_team.md", "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5000'\n---\nNotes about the Team.\n")]
    [InlineData("Home/_team.md", TeamWithoutLevel)]
    [InlineData("Plain/_team.md", TeamWithLevel)]
    [InlineData("Home/Repairs/_project.md", "---\nneedsYouLevel: 'run-inside-team'\n---\n")]
    [InlineData("Home/Repairs/_project.md", "Just notes, no frontmatter.\n")]
    public void Write_ChangingTheLevelOrCap_IsRefused(string relative, string content)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Equal(Refusal, this.guard.RefusalFor(Write(this.InTeams(relative), content)));
    }

    /// <summary>Creating a Project file that carries a level is refused, a missing file counting as unset; creating one without a level is allowed.</summary>
    [Fact]
    public void Write_ANewProjectFile_IsRefusedOnlyWhenItCarriesALevel()
    {
        string path = this.InTeams("Home/Fresh/_project.md");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The path has no folder."));

        Assert.Equal(Refusal, this.guard.RefusalFor(Write(path, "---\nneedsYouLevel: 'run-inside-team'\n---\n")));
        Assert.Null(this.guard.RefusalFor(Write(path, "---\navatarLabel: 'FR'\n---\n")));
    }

    /// <summary>A whole-file write that keeps the level and the cap as they are on disk, changing only the notes, is allowed.</summary>
    [Fact]
    public void Write_KeepingTheLevelAndCap_IsAllowed()
    {
        string content = "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5'\nowner: 'Ada'\n---\nNew notes.\n";

        Assert.Null(this.guard.RefusalFor(Write(this.InTeams("Home/_team.md"), content)));
        Assert.Null(this.guard.RefusalFor(Write(this.InTeams("Plain/_team.md"), TeamWithoutLevel + "More.\n")));
    }

    /// <summary>A read of these files is never refused.</summary>
    /// <param name="relative">The file below the Teams root.</param>
    [Theory]
    [InlineData("Home/_team.md")]
    [InlineData("Home/Repairs/_project.md")]
    public void Read_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Read, ToolCallStatus.Pending, JsonSerializer.Serialize(new { file_path = this.InTeams(relative) }));

        Assert.Null(this.guard.RefusalFor(call));
    }

    /// <summary>A relative path resolves against the Agent's Work Dir, and the path is compared ignoring case.</summary>
    [Fact]
    public void Edit_ThroughARelativeOrDifferentlyCasedPath_IsRefused()
    {
        // workDir = sandbox/Agents/Nova/work, so ../../../Teams/Home/_team.md = sandbox/Teams/Home/_team.md.
        string relative = "../../../Teams/Home/_team.md";
        string recased = Path.Combine(this.teamsRoot, "HOME", "_TEAM.MD");

        Assert.Equal(Refusal, this.guard.RefusalFor(Edit(relative, "needsYouLevel", "x")));
        Assert.Equal(Refusal, this.guard.RefusalFor(Edit(recased, "needsYouLevel", "x")));
    }

    /// <summary>Deleting or moving a file that holds a level or a cap is refused, and so is moving another file onto it.</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="relative">The file below the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "Home/_team.md")]
    [InlineData(ToolKind.Move, "Home/_team.md")]
    [InlineData(ToolKind.Delete, "Home/Repairs/_project.md")]
    [InlineData(ToolKind.Move, "Home/Repairs/_project.md")]
    public void Remove_OfAFileCarryingALevel_IsRefused(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        ToolCallInfo call = new("call-1", "Tool", kind, ToolCallStatus.Pending, JsonSerializer.Serialize(new { file_path = this.InTeams(relative) }));

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>Deleting a Team file that carries no level is not this guard's business.</summary>
    [Fact]
    public void Delete_OfAFileCarryingNoLevel_IsAllowed()
    {
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Delete, ToolCallStatus.Pending, JsonSerializer.Serialize(new { file_path = this.InTeams("Plain/_team.md") }));

        Assert.Null(this.guard.RefusalFor(call));
    }

    /// <summary>Moving a file that carries a level onto a Team file that has none sets that level, so it is refused.</summary>
    [Fact]
    public void Move_OfALevelFileOntoAGuardedFile_IsRefused()
    {
        string source = Path.Combine(this.sandbox, "elsewhere.md");
        File.WriteAllText(source, TeamWithLevel);
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Move, ToolCallStatus.Pending, JsonSerializer.Serialize(new { old_path = source, new_path = this.InTeams("Plain/_team.md") }));

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>A file with the same name outside the two guarded depths, in a reserved folder, or elsewhere is an ordinary file.</summary>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("Home/memory/_project.md")]
    [InlineData("Home/Repairs/Deep/_team.md")]
    [InlineData("Home/notes.md")]
    [InlineData("_team.md")]
    public void Edit_OfAFileOutsideTheGuardedPlaces_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Edit(this.InTeams(relative), "needsYouLevel", "spendCap")));
    }

    /// <summary>Through the permission handler the guard refuses the edit, rejects the call and logs one warning; another edit is approved.</summary>
    [Fact]
    public async Task Handler_RefusesTheLevelEdit_AndLogsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<WorkDirPermissionHandler> logger = new();
        WorkDirPermissionHandler handler = this.Handler(logger);

        PermissionDecision refused = await handler.DecideAsync(Request(Edit(this.InTeams("Home/_team.md"), "needsYouLevel: 'run-inside-team'", "needsYouLevel: 'ask-me'")), ct);
        PermissionDecision allowed = await handler.DecideAsync(Request(Edit(this.InTeams("Home/_team.md"), "Notes about the Team.", "Better.")), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(refused).OptionId);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(allowed).OptionId);
        Assert.Equal($"Refused a tool call for '' on '{this.InTeams("Home/_team.md")}': {Refusal}", Assert.Single(logger.AtExactly(LogLevel.Warning)).Message);
    }

    /// <summary>The guard is consulted right after the <c>AGENTS.md</c> guard.</summary>
    [Fact]
    public void InOrder_PlacesTheGuardRightAfterAgentsMd()
    {
        IReadOnlyList<IToolCallGuard> guards = this.Guards();

        Assert.IsType<AgentsMdGuard>(guards[0]);
        Assert.Same(this.guard, guards[1]);
    }

    private IReadOnlyList<IToolCallGuard> Guards()
    {
        FakePromptSource prompts = new();
        string inert = Path.Combine(this.sandbox, "Inert");
        return PermissionGuardList.InOrder(
            new AgentsMdGuard(this.teamsRoot, this.workDir, prompts),
            new TeamMemoryWriteGuard(inert, this.workDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            new RawGuard(inert, Path.Combine(inert, "Raw"), Path.Combine(inert, "Archive"), this.workDir, prompts),
            new PromptsRootGuard(Path.Combine(inert, "Prompts"), this.workDir, prompts),
            needsYouLevel: this.guard);
    }

    private WorkDirPermissionHandler Handler(ILogger<WorkDirPermissionHandler> logger) =>
        new(logger, this.Guards(), workDir: this.workDir);

    private static PermissionRequestContext Request(ToolCallInfo call) =>
        new(
            "session-1",
            call,
            [
                new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
                new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
            ]);

    private string InTeams(string relative) =>
        Path.Combine(this.teamsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static ToolCallInfo Edit(string path, string oldText, string newText) =>
        new("call-1", "Edit", ToolKind.Edit, ToolCallStatus.Pending, JsonSerializer.Serialize(new { file_path = path, old_string = oldText, new_string = newText }));

    private static ToolCallInfo Write(string path, string content) =>
        new("call-1", "Write", ToolKind.Edit, ToolCallStatus.Pending, JsonSerializer.Serialize(new { file_path = path, content }));
}
