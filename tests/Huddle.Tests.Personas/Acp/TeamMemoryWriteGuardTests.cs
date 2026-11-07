namespace Agency.Huddle.Tests.Acp;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamMemoryWriteGuard"/> through <see cref="WorkDirPermissionHandler"/> (Instructions spec §5.6): an Agent's own
/// <c>Edit</c>, <c>Delete</c> or <c>Move</c> inside a Team's or Project's <c>memory</c> folder is refused unless the Persona is
/// that Team's manager or the Chief of Staff, while a read is allowed and every other path is left as it was.
/// </summary>
public sealed class TeamMemoryWriteGuardTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> BothKinds =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-tmguard");

    private static readonly string TeamsRoot = Path.Combine(Root, "Teams");

    private static readonly string WorkDir = Path.Combine(Root, "Agents", "Leo", "work");

    /// <summary>Builds the handler for a Persona: Dev (manager Ada, member Leo, Project Apollo), Ops (no manager, member Leo) and a Chief of Staff.</summary>
    /// <param name="persona">The Persona the handler answers for.</param>
    /// <param name="logger">Receives the handler's log entries.</param>
    private static WorkDirPermissionHandler Handler(string persona, ILogger<WorkDirPermissionHandler> logger)
    {
        FakeTeamCatalog catalog = new()
        {
            Teams =
            [
                new TeamSummary("Dev", ["Apollo"], ["Leo"], true) { Manager = "Ada" },
                new TeamSummary("Ops", [], ["Leo"], true),
                new TeamSummary("..x", [], ["Leo"], true) { Manager = "Ada" },
            ],
        };
        TeamMemoryAccess access = new(catalog, static name => string.Equals(name, "Chief", StringComparison.Ordinal));
        FakePromptSource prompts = new();

        return new WorkDirPermissionHandler(
            logger,
            OnlyTeamMemory(new TeamMemoryWriteGuard(TeamsRoot, WorkDir, access, prompts)),
            personaName: persona);
    }

    /// <summary>
    /// Wraps the Team Memory guard under test with an AGENTS.md guard and a Raw guard rooted in an unrelated folder, so only the Team Memory
    /// guard can refuse a path under <see cref="TeamsRoot"/>.
    /// </summary>
    /// <param name="teamMemory">The guard under test.</param>
    private static IReadOnlyList<IToolCallGuard> OnlyTeamMemory(TeamMemoryWriteGuard teamMemory)
    {
        string inert = Path.Combine(Root, "Inert");
        FakePromptSource prompts = new();
        return PermissionGuardList.InOrder(
            new AgentsMdGuard(inert, WorkDir, prompts),
            teamMemory,
            new RawGuard(inert, Path.Combine(inert, "Raw"), Path.Combine(inert, "Archive"), WorkDir, prompts),
            new PromptsRootGuard(Path.Combine(inert, "Prompts"), WorkDir, prompts),
            protectedDirectory: new ProtectedDirectoryGuard(Path.Combine(Root, ".claude")));
    }

    /// <summary>Decides a tool call of one kind on one path and returns the chosen option id.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static async Task<string> DecideAsync(WorkDirPermissionHandler handler, ToolKind kind, string path)
    {
        PermissionRequestContext request = new(
            "session-1",
            new ToolCallInfo("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}"""),
            BothKinds);
        PermissionDecision decision = await handler.DecideAsync(request, TestContext.Current.CancellationToken);
        return Assert.IsType<SelectedDecision>(decision).OptionId;
    }

    /// <summary>A member's edit, delete or move in a Team's or Project's memory folder is refused.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "Dev/memory/x.md")]
    [InlineData(ToolKind.Delete, "Dev/memory/x.md")]
    [InlineData(ToolKind.Move, "Dev/memory/x.md")]
    [InlineData(ToolKind.Edit, "Dev/Apollo/memory/x.md")]
    [InlineData(ToolKind.Edit, "dev/MEMORY/x.md")]
    [InlineData(ToolKind.Delete, "Dev/memory")]
    [InlineData(ToolKind.Edit, "Ops/memory/x.md")]
    public async Task Member_WriteInTeamMemory_IsRefused(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), kind, Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>A member's read of the same files is allowed (U-5).</summary>
    [Fact]
    public async Task Member_ReadOfTeamMemory_IsAllowed()
    {
        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Read, Path.Combine(TeamsRoot, "Dev", "memory", "x.md"));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>The manager and the Chief of Staff write the Team's and a Project's memory.</summary>
    /// <param name="persona">The Persona.</param>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("Ada", "Dev/memory/x.md")]
    [InlineData("Ada", "Dev/Apollo/memory/x.md")]
    [InlineData("Chief", "Dev/memory/x.md")]
    [InlineData("Chief", "Ops/memory/x.md")]
    public async Task ManagerAndChiefOfStaff_WriteInTeamMemory_IsAllowed(string persona, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler(persona, NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A Persona in no Team is refused too, since only the manager and the Chief of Staff write.</summary>
    [Fact]
    public async Task Outsider_WriteInTeamMemory_IsRefused()
    {
        string chosen = await DecideAsync(Handler("Zed", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(TeamsRoot, "Dev", "memory", "x.md"));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Everything else stays as it was: the Agent's own memory, a Team's other files, a folder that is no Team, an unregistered Project, and a relative path outside.</summary>
    /// <param name="path">The path written to, absolute under the temp root or relative to the Work Dir.</param>
    [Theory]
    [InlineData("Agents/Leo/work/memory/x.md")]
    [InlineData("Teams/Dev/Process.md")]
    [InlineData("Teams/Dev/Apollo/notes.md")]
    [InlineData("Teams/Other/memory/x.md")]
    [InlineData("Teams/Dev/Notes/memory/x.md")]
    [InlineData("elsewhere/Dev/memory/x.md")]
    public async Task Member_WriteElsewhere_IsAllowed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A relative path is resolved against the Work Dir, so a climb out of it into a Team's memory is caught.</summary>
    [Fact]
    public async Task Member_RelativeClimbIntoTeamMemory_IsRefused()
    {
        string relative = Path.Combine("..", "..", "..", "Teams", "Dev", "memory", "x.md");

        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, relative);

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>A Team whose folder name merely starts with two dots is a Team, not a climb out of the Teams root, so a member is still refused there.</summary>
    [Fact]
    public async Task Member_WriteInATeamNamedWithLeadingDots_IsRefused()
    {
        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(TeamsRoot, "..x", "memory", "x.md"));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Windows only: even the manager's write through a short name or a stream name is refused, since the guard cannot tell where it lands.</summary>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("Dev/MEMORY~1/x.md")]
    [InlineData("Dev/memory/x.md:stream")]
    public async Task Manager_WriteThroughAnAlias_IsRefused(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler("Ada", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>Deleting or moving a folder that holds a Memory folder takes the Memory with it, so a Persona who may not write it is refused: the Teams root, the Team folder, a Project folder and the memory folder itself.</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="persona">The Persona.</param>
    /// <param name="relative">The path under the Teams root, empty for the root itself.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "Leo", "")]
    [InlineData(ToolKind.Move, "Leo", "")]
    [InlineData(ToolKind.Delete, "Leo", "Dev")]
    [InlineData(ToolKind.Move, "Leo", "Dev")]
    [InlineData(ToolKind.Delete, "Leo", "Dev/Apollo")]
    [InlineData(ToolKind.Move, "Leo", "Dev/Apollo")]
    [InlineData(ToolKind.Delete, "Leo", "Dev/memory")]
    [InlineData(ToolKind.Delete, "Ada", "")]
    [InlineData(ToolKind.Delete, "Ada", "Ops")]
    public async Task DeleteOrMove_OfAFolderAboveTeamMemory_IsRefusedUnlessTheCallerWritesIt(ToolKind kind, string persona, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler(persona, NullLogger<WorkDirPermissionHandler>.Instance), kind, Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>
    /// Windows only: a folder named in another letter case is the same folder on a case-insensitive file system, so moving
    /// <c>dev/apollo</c> is refused like <c>Dev/Apollo</c>. On Linux it is a different folder that holds no Memory.
    /// </summary>
    [Fact]
    public async Task Move_OfAFolderAboveTeamMemoryInAnotherCase_IsRefusedOnWindows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only a case-insensitive file system makes dev/apollo the Team's Dev/Apollo.");

        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Move, Path.Combine(TeamsRoot, "dev", "apollo"));

        Assert.Equal("reject-1", chosen);
    }

    /// <summary>The manager of every Team a folder holds, and the Chief of Staff, may delete or move it; a folder holding no Memory is anyone's.</summary>
    /// <param name="persona">The Persona.</param>
    /// <param name="relative">The path under the Teams root, empty for the root itself.</param>
    [Theory]
    [InlineData("Ada", "Dev")]
    [InlineData("Ada", "Dev/Apollo")]
    [InlineData("Chief", "")]
    [InlineData("Chief", "Dev")]
    [InlineData("Leo", "Dev/Apollo/notes")]
    [InlineData("Leo", "Other")]
    public async Task DeleteOrMove_ByAnAuthorisedPersonaOrOfNoMemory_IsAllowed(string persona, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler(persona, NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Delete, Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A member's edit of a folder path, which changes no file under it, is not a delete: only a delete or a move reaches what is below.</summary>
    [Fact]
    public async Task Edit_OfAFolderAboveTeamMemory_IsAllowed()
    {
        string chosen = await DecideAsync(Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(TeamsRoot, "Dev"));

        Assert.Equal("allow-1", chosen);
    }

    /// <summary>A move's destination is read: moving a file into a Team's memory folder, under any destination argument name, is refused for a member.</summary>
    /// <param name="argument">The destination argument's name.</param>
    [Theory]
    [InlineData("new_path")]
    [InlineData("destination")]
    [InlineData("target")]
    [InlineData("to")]
    public async Task Move_IntoTeamMemory_IsRefusedForAMember(string argument)
    {
        string source = JsonSerializer.Serialize(Path.Combine(Root, "elsewhere", "a.md"));
        string destination = JsonSerializer.Serialize(Path.Combine(TeamsRoot, "Dev", "memory", "a.md"));
        PermissionRequestContext request = new(
            "session-1",
            new ToolCallInfo("call-1", "Tool", ToolKind.Move, ToolCallStatus.Pending, $$"""{"old_path":{{source}},"{{argument}}":{{destination}}}"""),
            BothKinds);

        PermissionDecision decision = await Handler("Leo", NullLogger<WorkDirPermissionHandler>.Instance).DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
    }

    /// <summary>Windows only: a short name or a stream outside the Teams root is an ordinary path and is allowed; one inside it is still refused.</summary>
    /// <param name="relative">The path under the temp root.</param>
    /// <param name="expected">The option id expected.</param>
    [Theory]
    [InlineData("elsewhere/RUNNER~1/notes.md", "allow-1")]
    [InlineData("Agents/Leo/work/notes~2.md", "allow-1")]
    [InlineData("Teams/Dev/MEMORY~1/x.md", "reject-1")]
    public async Task Write_ThroughAnAlias_IsRefusedOnlyInsideTheTeamsRoot(string relative, string expected)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);

        string chosen = await DecideAsync(Handler("Ada", NullLogger<WorkDirPermissionHandler>.Instance), ToolKind.Edit, Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(expected, chosen);
    }

    /// <summary>One permission decision looks the Persona up once, however many Teams the path reaches.</summary>
    [Fact]
    public async Task Decision_ReachingEveryTeam_ResolvesTheChiefOfStaffOnce()
    {
        int lookups = 0;
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Dev", [], ["Leo"], true) { Manager = "Ada" }, new TeamSummary("Ops", [], ["Leo"], true), new TeamSummary("Qa", [], ["Leo"], true)],
        };
        TeamMemoryAccess access = new(catalog, name => { lookups++; return string.Equals(name, "Chief", StringComparison.Ordinal); });
        WorkDirPermissionHandler handler = new(
            NullLogger<WorkDirPermissionHandler>.Instance,
            OnlyTeamMemory(new TeamMemoryWriteGuard(TeamsRoot, WorkDir, access, new FakePromptSource())),
            personaName: "Chief");

        string chosen = await DecideAsync(handler, ToolKind.Delete, TeamsRoot);

        Assert.Equal("allow-1", chosen);
        Assert.Equal(1, lookups);
    }

    /// <summary>Listing the Teams a Persona reads looks the Persona up once, not once per Team.</summary>
    [Fact]
    public void Readable_OverSeveralTeams_ResolvesTheChiefOfStaffOnce()
    {
        int lookups = 0;
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Dev", [], ["Leo"], true), new TeamSummary("Ops", [], ["Leo"], true), new TeamSummary("Qa", [], ["Zed"], true)],
        };
        TeamMemoryAccess access = new(catalog, name => { lookups++; return false; });

        IReadOnlyList<TeamSummary> readable = access.Readable("Leo");

        Assert.Equal(2, readable.Count);
        Assert.Equal(1, lookups);
    }

    /// <summary>The refusal is logged with the reason, naming the manager (or saying the Team has none).</summary>
    [Fact]
    public async Task Refusal_IsLoggedWithTheManagersName()
    {
        RecordingLogger<WorkDirPermissionHandler> logger = new();
        WorkDirPermissionHandler handler = Handler("Leo", logger);

        string dev = Path.Combine(TeamsRoot, "Dev", "memory", "x.md");
        string ops = Path.Combine(TeamsRoot, "Ops", "memory", "x.md");
        _ = await DecideAsync(handler, ToolKind.Edit, dev);
        _ = await DecideAsync(handler, ToolKind.Edit, ops);

        IReadOnlyList<LogEntry> warnings = logger.AtExactly(LogLevel.Warning);
        Assert.Equal(2, warnings.Count);
        Assert.Equal($"Refused a tool call for 'Leo' on '{dev}': Only Dev's manager, Ada, writes Dev's Team Memory. Tell Ada, or keep it in your own Memory.", warnings[0].Message);
        Assert.Equal($"Refused a tool call for 'Leo' on '{ops}': Ops has no manager, so its Team Memory is written by the Human.", warnings[1].Message);
    }
}
