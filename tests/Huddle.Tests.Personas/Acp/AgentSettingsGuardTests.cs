using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="AgentSettingsGuard"/> (issue #432): an Agent's path-argument write to its Work Dir's <c>.claude/settings.local.json</c>, to
/// <c>merge-gate-ask.js</c> or into Huddle's hooks folder is refused, and an ordinary file, a project's own <c>.mcp.json</c>,
/// <c>.claude/settings.json</c> and <c>.claude/hooks/*</c> are left alone.
/// </summary>
public sealed class AgentSettingsGuardTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-settingsguard");

    private static readonly string WorkDir = Path.Combine(Root, "work");

    private static readonly string HooksDir = Path.Combine(Root, "data", "hooks");

    private static readonly AgentSettingsGuard Guard = new(WorkDir, HooksDir);

    private static readonly IReadOnlyList<PermissionOptionInfo> Offered =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    /// <summary>Builds a tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    /// <param name="toolName">The tool name the Adapter reported, or null.</param>
    private static ToolCallInfo Call(ToolKind kind, string path, string? toolName = null) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""", toolName);

    /// <summary>The guarded files are refused when edited, by a relative or an absolute path, whatever the case.</summary>
    /// <param name="path">The path argument.</param>
    [Theory]
    [InlineData(".claude/settings.local.json")]
    [InlineData(".CLAUDE/Settings.Local.json")]
    [InlineData("sub/../.claude/settings.local.json")]
    [InlineData("elsewhere/merge-gate-ask.js")]
    public void RefusalFor_GuardedFile_IsRefused(string path) =>
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Edit, path)));

    /// <summary>An absolute path to the settings file and a file in Huddle's hooks folder are refused as well.</summary>
    [Fact]
    public void RefusalFor_AbsolutePaths_AreRefused()
    {
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(WorkDir, ".claude", "settings.local.json"))));
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(HooksDir, "doc-history-stop.js"))));
    }

    /// <summary>Deleting or moving the file, or the folder that holds it, is refused too.</summary>
    [Fact]
    public void RefusalFor_DeleteOrMove_OfTheFileOrItsFolder_IsRefused()
    {
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Delete, ".claude/settings.local.json")));
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Move, ".claude/settings.local.json")));
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Delete, ".claude")));
        Assert.NotNull(Guard.RefusalFor(Call(ToolKind.Delete, Path.Combine(HooksDir, "merge-gate-ask.js"))));
    }

    /// <summary>
    /// An ordinary file, a project's own <c>.mcp.json</c>, <c>.claude/settings.json</c> and <c>.claude/hooks/*</c>, and a read of the
    /// guarded file are all allowed: a development Team's Agent may edit its repository's own files.
    /// </summary>
    /// <param name="path">The path argument.</param>
    [Theory]
    [InlineData("notes.txt")]
    [InlineData(".mcp.json")]
    [InlineData(".claude/settings.json")]
    [InlineData(".claude/hooks/pre-commit.sh")]
    [InlineData("src/settings.local.json")]
    public void RefusalFor_OrdinaryFile_IsAllowed(string path) =>
        Assert.Null(Guard.RefusalFor(Call(ToolKind.Edit, path)));

    /// <summary>A read of the settings file is never refused.</summary>
    [Fact]
    public void RefusalFor_ReadOfTheSettingsFile_IsAllowed() =>
        Assert.Null(Guard.RefusalFor(Call(ToolKind.Read, ".claude/settings.local.json")));

    /// <summary>A scope that records whether the Human was asked.</summary>
    private sealed class CountingScope : IElicitationScope
    {
        public int Asked { get; private set; }

        public Task<ElicitationResult> ElicitAsync(ElicitationRequest request, CancellationToken cancellationToken)
        {
            this.Asked++;
            return Task.FromResult<ElicitationResult>(new ElicitationCancelled());
        }
    }

    /// <summary>
    /// A call a guard refuses is refused before the connector ask: the handler refuses the settings write, and the Human is never asked,
    /// even when the Adapter names the call as a connector tool. An ordinary write is approved.
    /// </summary>
    [Fact]
    public async Task Handler_GuardRefusal_PrecedesAConnectorAsk()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakePromptSource prompts = new();
        string inert = Path.Combine(Root, "inert");
        PermissionGuards guards = new(
            new AgentsMdGuard(inert, WorkDir, prompts),
            new TeamMemoryWriteGuard(inert, WorkDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            new RawGuard(inert, Path.Combine(inert, "Raw"), Path.Combine(inert, "Archive"), WorkDir, prompts),
            new PromptsRootGuard(Path.Combine(inert, "Prompts"), WorkDir, prompts),
            Guard);
        WorkDirPermissionHandler handler = new(null, NullLogger<WorkDirPermissionHandler>.Instance, personaName: "Nova", guards: guards, workDir: WorkDir);
        CountingScope scope = new();

        PermissionDecision refused = await handler.DecideAsync(
            new PermissionRequestContext("session-1", Call(ToolKind.Edit, ".claude/settings.local.json", "mcp__claude_ai_ms365__x"), Offered, scope), ct);
        PermissionDecision ordinary = await handler.DecideAsync(
            new PermissionRequestContext("session-1", Call(ToolKind.Edit, "notes.txt"), Offered, scope), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(refused).OptionId);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(ordinary).OptionId);
        Assert.Equal(0, scope.Asked);
    }
}
