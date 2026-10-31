namespace Agency.Huddle.Tests.Acp;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="InstructionsWriteGuard"/> through <see cref="WorkDirPermissionHandler"/> (Instructions spec §3.3):
/// an Agent's edit, delete or move of an <c>Instructions.md</c> is refused at the Teams root, in a Team folder and in
/// its own Work Dir, while a read of the same file is allowed.
/// </summary>
public sealed class InstructionsWriteGuardTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> BothKinds =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    private static readonly string TeamsRoot = Path.Combine(Path.GetTempPath(), "huddle-guard", "Teams");

    private static readonly string WorkDir = Path.Combine(Path.GetTempPath(), "huddle-guard", "Agents", "Nova", "work");

    /// <summary>Builds the handler under test with the guard on and the <c>~/.claude</c> directory pointed somewhere harmless.</summary>
    /// <param name="logger">Receives the handler's log entries.</param>
    private static WorkDirPermissionHandler Handler(ILogger<WorkDirPermissionHandler> logger) =>
        new(
            Path.Combine(Path.GetTempPath(), "huddle-guard", ".claude"),
            logger,
            instructionsGuard: new InstructionsWriteGuard(TeamsRoot, WorkDir, new FakePromptSource()));

    /// <summary>Builds a permission request for a tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static PermissionRequestContext Request(ToolKind kind, string path) =>
        new(
            "session-1",
            new ToolCallInfo("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}"""),
            BothKinds);

    /// <summary>Decides one request and returns the chosen option id.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="request">The request.</param>
    private static async Task<string> DecideAsync(WorkDirPermissionHandler handler, PermissionRequestContext request)
    {
        PermissionDecision decision = await handler.DecideAsync(request, TestContext.Current.CancellationToken);
        return Assert.IsType<SelectedDecision>(decision).OptionId;
    }

    /// <summary>An edit, delete or move of an Instructions file is refused at each of the three places.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="place">Which place: huddle, team or agent.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "huddle")]
    [InlineData(ToolKind.Delete, "huddle")]
    [InlineData(ToolKind.Move, "huddle")]
    [InlineData(ToolKind.Edit, "team")]
    [InlineData(ToolKind.Delete, "team")]
    [InlineData(ToolKind.Move, "team")]
    [InlineData(ToolKind.Edit, "agent")]
    [InlineData(ToolKind.Delete, "agent")]
    [InlineData(ToolKind.Move, "agent")]
    public async Task Write_ToAnInstructionsFile_IsRefused(ToolKind kind, string place)
    {
        string path = PathFor(place, "Instructions.md");

        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), Request(kind, path));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Reading an Instructions file is never refused, whatever the place.</summary>
    /// <param name="place">Which place: huddle, team or agent.</param>
    [Theory]
    [InlineData("huddle")]
    [InlineData("team")]
    [InlineData("agent")]
    public async Task Read_OfAnInstructionsFile_IsAllowed(string place)
    {
        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), Request(ToolKind.Read, PathFor(place, "Instructions.md")));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>The name is compared ignoring case.</summary>
    [Fact]
    public async Task Write_ToAnInstructionsFileInAnotherCase_IsRefused()
    {
        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), Request(ToolKind.Edit, PathFor("team", "INSTRUCTIONS.md")));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>A relative path is resolved against the Agent's Work Dir, the Adapter's working directory.</summary>
    [Fact]
    public async Task Write_ToARelativeInstructionsPath_ResolvesAgainstTheWorkDir()
    {
        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), Request(ToolKind.Edit, "Instructions.md"));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Other files in the same places, and an Instructions file in other places, are written as before.</summary>
    /// <param name="path">The path written to.</param>
    [Theory]
    [InlineData("work/notes.md")]
    [InlineData("Teams/Dev/Process.md")]
    [InlineData("Teams/Dev/Project/Instructions.md")]
    [InlineData("Agents/Nova/work/memory/Instructions.md")]
    [InlineData("elsewhere/Instructions.md")]
    public async Task Write_ToAnythingElse_IsAllowed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string full = Path.Combine(Path.GetTempPath(), "huddle-guard", path.Replace('/', Path.DirectorySeparatorChar));

        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), Request(ToolKind.Edit, full));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A refusal is logged as a Warning naming the file, since the permission reply carries no reason.</summary>
    [Fact]
    public async Task Refusal_IsLoggedWithTheReason()
    {
        RecordingLogger<WorkDirPermissionHandler> logger = new();

        _ = await DecideAsync(Handler(logger), Request(ToolKind.Edit, PathFor("huddle", "Instructions.md")));

        string message = Assert.Single(logger.AtExactly(LogLevel.Warning)).Message;
        Assert.Equal($"Refused a tool call for '': Instructions files are written by the Human. Ask the Human to change {Path.Combine(TeamsRoot, "Instructions.md")}.", message);
    }

    /// <summary>Windows only: a write through an 8.3 short name or an alternate data stream cannot be compared by name, so it is refused and logged.</summary>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("INSTRU~1.MD")]
    [InlineData("DEV~1/Instructions.md")]
    [InlineData("Instructions.md:stream")]
    public async Task Write_ThroughAnAlias_IsRefusedAndLogged(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);
        RecordingLogger<WorkDirPermissionHandler> logger = new();
        string path = Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        string chosen = await DecideAsync(Handler(logger), Request(ToolKind.Edit, path));

        Assert.Equal("reject-1", chosen);
        Assert.Equal(
            $"Refused a tool call for '': {path} goes through a short name or a stream name, so it cannot be checked. Write the file by its full name.",
            Assert.Single(logger.AtExactly(LogLevel.Warning)).Message);
    }

    /// <summary>When the profile directory cannot be resolved the handler has no <c>~/.claude</c> to protect, but the Instructions guard still refuses.</summary>
    [Fact]
    public async Task WithoutAProtectedDirectory_TheGuardStillRefuses()
    {
        WorkDirPermissionHandler handler = new(null, NullLogger<WorkDirPermissionHandler>.Instance, instructionsGuard: new InstructionsWriteGuard(TeamsRoot, WorkDir, new FakePromptSource()));

        string guarded = await DecideAsync(handler, Request(ToolKind.Edit, PathFor("huddle", "Instructions.md")));
        string other = await DecideAsync(handler, Request(ToolKind.Edit, PathFor("agent", "notes.md")));

        Assert.Equal("reject-1", guarded);
        Assert.Equal("allow-1", other);
    }

    /// <summary>Without the guard the handler approves such a write exactly as it always did.</summary>
    [Fact]
    public async Task WithoutTheGuard_TheWriteIsApproved()
    {
        WorkDirPermissionHandler handler = new(Path.Combine(Path.GetTempPath(), "huddle-guard", ".claude"), NullLogger<WorkDirPermissionHandler>.Instance);

        string chosen = await DecideAsync(handler, Request(ToolKind.Edit, PathFor("huddle", "Instructions.md")));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>The Agents' definitions root the other tests' Work Dir lives under: <c>huddle-guard/Agents</c>.</summary>
    private static AgentPaths Agents() =>
        new(Options.Create(new TeamOptions { DataDir = Path.Combine(Path.GetTempPath(), "huddle-guard") }));

    /// <summary>Builds the handler under test with the guard on and every Agent's Work Dir located, as the application builds it.</summary>
    private static WorkDirPermissionHandler HandlerWithAgents() =>
        new(
            Path.Combine(Path.GetTempPath(), "huddle-guard", ".claude"),
            NullLogger<WorkDirPermissionHandler>.Instance,
            instructionsGuard: new InstructionsWriteGuard(TeamsRoot, WorkDir, new FakePromptSource(), Agents()));

    /// <summary>Builds a permission request for a tool call of one kind carrying the given raw input.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="rawInputJson">The raw input.</param>
    private static PermissionRequestContext RequestJson(ToolKind kind, string rawInputJson) =>
        new("session-1", new ToolCallInfo("call-1", "Tool", kind, ToolCallStatus.Pending, rawInputJson), BothKinds);

    /// <summary>A path under the shared temp root.</summary>
    /// <param name="relative">The path below <c>huddle-guard</c>, with forward slashes.</param>
    private static string Under(string relative) =>
        Path.Combine(Path.GetTempPath(), "huddle-guard", relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Another Agent's <c>Instructions.md</c> is that Agent's highest-precedence level, so an edit, delete or move of it by this Agent is refused.</summary>
    /// <param name="kind">The write kind.</param>
    [Theory]
    [InlineData(ToolKind.Edit)]
    [InlineData(ToolKind.Delete)]
    [InlineData(ToolKind.Move)]
    public async Task Write_ToAnotherAgentsInstructions_IsRefused(ToolKind kind)
    {
        string chosen = await DecideAsync(HandlerWithAgents(), Request(kind, Under("Agents/Other/work/Instructions.md")));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Only the file directly in another Agent's Work Dir is guarded; its other files, its sub-folders and its definition folder are written as before.</summary>
    /// <param name="relative">The path below <c>huddle-guard</c>.</param>
    [Theory]
    [InlineData("Agents/Other/work/memory/Instructions.md")]
    [InlineData("Agents/Other/Instructions.md")]
    [InlineData("Agents/Other/work/notes.md")]
    public async Task Write_ToAnotherAgentsOtherFiles_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(HandlerWithAgents(), Request(ToolKind.Edit, Under(relative)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A move's destination is read: moving a file onto an Instructions file is refused, whichever argument name carries the destination.</summary>
    /// <param name="argument">The destination argument's name.</param>
    [Theory]
    [InlineData("new_path")]
    [InlineData("destination")]
    [InlineData("target")]
    public async Task Move_ToAnInstructionsFile_IsRefused(string argument)
    {
        string source = JsonSerializer.Serialize(Under("elsewhere/a.md"));
        string destination = JsonSerializer.Serialize(Path.Combine(TeamsRoot, "Dev", "Instructions.md"));

        string chosen = await DecideAsync(Handler(NullLogger<WorkDirPermissionHandler>.Instance), RequestJson(ToolKind.Move, $$"""{"old_path":{{source}},"{{argument}}":{{destination}}}"""));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Deleting or moving a folder that holds a guarded Instructions file takes the file with it, so it is refused.</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="relative">The path below <c>huddle-guard</c>.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "Teams")]
    [InlineData(ToolKind.Move, "Teams")]
    [InlineData(ToolKind.Delete, "Teams/Alpha")]
    [InlineData(ToolKind.Move, "Teams/Alpha")]
    [InlineData(ToolKind.Delete, "Agents/Nova/work")]
    [InlineData(ToolKind.Delete, "Agents/Nova")]
    [InlineData(ToolKind.Delete, "Agents")]
    [InlineData(ToolKind.Delete, "Agents/Other/work")]
    [InlineData(ToolKind.Move, "Agents/Other")]
    public async Task DeleteOrMove_OfAFolderHoldingInstructions_IsRefused(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        Directory.CreateDirectory(Under("Teams/Alpha"));

        string chosen = await DecideAsync(HandlerWithAgents(), Request(kind, Under(relative)));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>A delete of something that holds no Instructions file, an edit of a folder path, and a delete below the guarded files are left alone.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="relative">The path below <c>huddle-guard</c>.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "Teams/Alpha/Notes")]
    [InlineData(ToolKind.Delete, "Teams/loose.txt")]
    [InlineData(ToolKind.Delete, "Agents/Nova/work/memory")]
    [InlineData(ToolKind.Delete, "Agents/Nova/Nova.md")]
    [InlineData(ToolKind.Delete, "elsewhere")]
    [InlineData(ToolKind.Edit, "Teams/Alpha")]
    public async Task DeleteOrMove_OfSomethingHoldingNoInstructions_IsAllowed(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        Directory.CreateDirectory(Under("Teams/Alpha"));
        await File.WriteAllTextAsync(Under("Teams/loose.txt"), "x", TestContext.Current.CancellationToken);

        string chosen = await DecideAsync(HandlerWithAgents(), Request(kind, Under(relative)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>Windows only: an 8.3 short name or a stream outside the guarded files is an ordinary path, so the edit is allowed.</summary>
    /// <param name="relative">The path below <c>huddle-guard</c>.</param>
    [Theory]
    [InlineData("elsewhere/RUNNER~1/notes.md")]
    [InlineData("elsewhere/notes~2.md")]
    [InlineData("Agents/Nova/work/notes~2.md")]
    [InlineData("Agents/Nova/work/sub/DEV~1/x.md")]
    [InlineData("Teams/Dev/memory/NOTES~1.md")]
    public async Task Write_ThroughAnAliasOutsideTheGuardedFiles_IsAllowed(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(HandlerWithAgents(), Request(ToolKind.Edit, Under(relative)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>Windows only: a short name of an Instructions file in another Agent's Work Dir is refused like one in the Agent's own.</summary>
    [Fact]
    public async Task Write_ThroughAShortNameInAnotherAgentsWorkDir_IsRefused()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");

        string chosen = await DecideAsync(HandlerWithAgents(), Request(ToolKind.Edit, Under("Agents/Other/work/INSTRU~1.MD")));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>The path of <paramref name="fileName"/> in one of the three places.</summary>
    /// <param name="place">Which place: huddle, team or agent.</param>
    /// <param name="fileName">The file name.</param>
    private static string PathFor(string place, string fileName) => place switch
    {
        "huddle" => Path.Combine(TeamsRoot, fileName),
        "team" => Path.Combine(TeamsRoot, "Dev", fileName),
        _ => Path.Combine(WorkDir, fileName),
    };
}
