using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins how <see cref="WorkDirPermissionHandler"/> runs its ordered list of <see cref="IToolCallGuard"/>s: in the given order, the first
/// refusal stops the rest, a clean pass approves, a throwing guard never approves, and the production list is in the order the handler
/// used to hard-code.
/// </summary>
public sealed class ToolCallGuardListTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> Offered =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    private static PermissionRequestContext Request(IElicitationScope? scope = null, string? toolName = null) =>
        new("session-1", new ToolCallInfo("call-1", "Tool", toolName is null ? ToolKind.Edit : ToolKind.Other, ToolCallStatus.Pending, null, toolName), Offered, scope);

    private static WorkDirPermissionHandler Handler(params IToolCallGuard[] guards) =>
        new(NullLogger<WorkDirPermissionHandler>.Instance, guards, personaName: "Nova");

    /// <summary>A guard that records that it ran and answers with a fixed reason, or throws.</summary>
    /// <param name="name">What the guard writes to the shared order list.</param>
    /// <param name="order">The shared list.</param>
    /// <param name="reason">The refusal reason, or <see langword="null"/> to pass.</param>
    /// <param name="throws">Whether the guard throws instead of answering.</param>
    private sealed class RecordingGuard(string name, List<string> order, string? reason = null, bool throws = false) : IToolCallGuard
    {
        public string? PersonaSeen { get; private set; }

        public string? RefusalFor(ToolCallInfo toolCall, string personaName)
        {
            order.Add(name);
            this.PersonaSeen = personaName;
            if (throws)
            {
                throw new InvalidOperationException("guard failed");
            }

            return reason;
        }
    }

    /// <summary>A scope that counts the questions put to the Human.</summary>
    private sealed class CountingScope : IElicitationScope
    {
        public int Asked { get; private set; }

        public Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
        {
            this.Asked++;
            return Task.FromResult<ElicitationResult>(new ElicitationCancelled());
        }
    }

    /// <summary>Guards are consulted in the given order, with the Persona's name, and the first refusal stops the rest.</summary>
    [Fact]
    public async Task DecideAsync_FirstRefusal_StopsTheLaterGuards()
    {
        List<string> order = [];
        RecordingGuard first = new("first", order);
        RecordingGuard second = new("second", order, reason: "no");
        RecordingGuard third = new("third", order);

        PermissionDecision decision = await Handler(first, second, third).DecideAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Equal(["first", "second"], order);
        Assert.Equal("Nova", second.PersonaSeen);
    }

    /// <summary>When every guard passes, the call is approved and every guard ran, in order.</summary>
    [Fact]
    public async Task DecideAsync_AllGuardsPass_Approves()
    {
        List<string> order = [];

        PermissionDecision decision = await Handler(new RecordingGuard("a", order), new RecordingGuard("b", order)).DecideAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Equal(["a", "b"], order);
    }

    /// <summary>A guard that throws is not an approval: the exception reaches the caller and the later guards never run.</summary>
    [Fact]
    public async Task DecideAsync_GuardThrows_PropagatesAndIsNotApproval()
    {
        List<string> order = [];
        WorkDirPermissionHandler handler = Handler(new RecordingGuard("boom", order, throws: true), new RecordingGuard("after", order));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.DecideAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Equal(["boom"], order);
    }

    /// <summary>A connector call that a guard refuses is refused without asking the Human, through the list.</summary>
    [Fact]
    public async Task DecideAsync_RefusedConnectorCall_NeverAsksTheHuman()
    {
        CountingScope scope = new();
        WorkDirPermissionHandler handler = Handler(new RecordingGuard("g", [], reason: "no"));

        PermissionDecision decision = await handler.DecideAsync(Request(scope, "mcp__claude_ai_ms365__outlook_send_mail"), TestContext.Current.CancellationToken);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(decision).OptionId);
        Assert.Equal(0, scope.Asked);
    }

    /// <summary>The production list holds the guards in exactly the order the handler used to hard-code, with the optional ones present when given.</summary>
    [Fact]
    public void InOrder_HoldsTheGuardsInTheOrderTheHandlerUsedToCheckThem()
    {
        FakePromptSource prompts = new();
        string root = Path.Combine(Path.GetTempPath(), "huddle-guardlist");
        string workDir = Path.Combine(root, "work");

        IReadOnlyList<IToolCallGuard> guards = PermissionGuardList.InOrder(
            new AgentsMdGuard(root, workDir, prompts),
            new TeamMemoryWriteGuard(root, workDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            new RawGuard(root, Path.Combine(root, "Raw"), Path.Combine(root, "Archive"), workDir, prompts),
            new PromptsRootGuard(Path.Combine(root, "Prompts"), workDir, prompts),
            new AgentSettingsGuard(workDir),
            new ProtectedDirectoryGuard(Path.Combine(root, ".claude"), workDir),
            new MergeGateGuard(new NoGate()));

        Assert.Equal(
            [
                typeof(ProtectedDirectoryGuard),
                typeof(AgentsMdGuard),
                typeof(RawGuard),
                typeof(PromptsRootGuard),
                typeof(TeamMemoryWriteGuard),
                typeof(AgentSettingsGuard),
                typeof(MergeGateGuard),
            ],
            guards.Select(static guard => guard.GetType()).ToList());
    }

    /// <summary>A missing protected directory, settings guard or merge gate leaves that guard out of the list.</summary>
    [Fact]
    public void InOrder_WithoutTheOptionalGuards_OmitsThem()
    {
        FakePromptSource prompts = new();
        string root = Path.Combine(Path.GetTempPath(), "huddle-guardlist");
        string workDir = Path.Combine(root, "work");

        IReadOnlyList<IToolCallGuard> guards = PermissionGuardList.InOrder(
            new AgentsMdGuard(root, workDir, prompts),
            new TeamMemoryWriteGuard(root, workDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            new RawGuard(root, Path.Combine(root, "Raw"), Path.Combine(root, "Archive"), workDir, prompts),
            new PromptsRootGuard(Path.Combine(root, "Prompts"), workDir, prompts));

        Assert.Equal(
            [typeof(AgentsMdGuard), typeof(RawGuard), typeof(PromptsRootGuard), typeof(TeamMemoryWriteGuard)],
            guards.Select(static guard => guard.GetType()).ToList());
    }

    /// <summary>The protected-directory guard names the path it found and the directory in its reason, and passes a path outside.</summary>
    [Fact]
    public void ProtectedDirectoryGuard_RefusesInsideWithTheReason_AndPassesOutside()
    {
        string root = Path.Combine(Path.GetTempPath(), "huddle-guardlist", ".claude");
        string inside = Path.Combine(root, "memory", "x.md");
        ProtectedDirectoryGuard guard = new(root);
        ToolCallInfo Call(string path) => new("c1", "Write", ToolKind.Edit, ToolCallStatus.Pending, $$"""{"file_path":{{System.Text.Json.JsonSerializer.Serialize(path)}}}""");

        Assert.Equal($"'{inside}' is inside the agent's own configuration directory '{root}'.", guard.RefusalFor(Call(inside), "Nova"));
        Assert.Null(guard.RefusalFor(Call(Path.Combine(Path.GetTempPath(), "huddle-guardlist", "notes.md")), "Nova"));
    }

    /// <summary>The merge-gate guard asks the gate only for a merge or push, and carries the command and the gate's reason in its own.</summary>
    [Fact]
    public void MergeGateGuard_RefusesAPushWithTheGatesReason_AndLeavesOtherCommandsAlone()
    {
        MergeGateGuard guard = new(new RefusingGate());
        ToolCallInfo Call(string command) => new("c1", "Bash", ToolKind.Execute, ToolCallStatus.Pending, $$"""{"command":{{System.Text.Json.JsonSerializer.Serialize(command)}}}""");

        Assert.Equal("'git push' is held by the merge gate: needs evidence.", guard.RefusalFor(Call("git push"), "Nova"));
        Assert.Null(guard.RefusalFor(Call("git status"), "Nova"));
    }

    private sealed class RefusingGate : IMergeGate
    {
        public string? RefusalFor(string personaName, string command) => "needs evidence.";
    }

    private sealed class NoGate : IMergeGate
    {
        public string? RefusalFor(string personaName, string command) => null;
    }
}
