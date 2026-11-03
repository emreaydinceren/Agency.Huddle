using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins that <see cref="WorkDirPermissionHandler"/> consults a <see cref="RawGuard"/> given to it (Spec §8.6).</summary>
public sealed class RawGuardWiringTests
{
    private static readonly IReadOnlyList<PermissionOptionInfo> BothKinds =
    [
        new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
        new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
    ];

    /// <summary>A handler built with a Raw guard rejects a delete under <c>Teams/Home/raw</c>, and one built without it approves the same call.</summary>
    [Fact]
    public async Task DecideAsync_DeleteUnderTeamRaw_IsRejectedOnlyWithTheGuard()
    {
        string root = Path.Combine(Path.GetTempPath(), "huddle-rawwiring");
        string teamsRoot = Path.Combine(root, "Teams");
        string workDir = Path.Combine(root, "Agents", "Nova", "work");
        FakePromptSource prompts = new();
        RawGuard guard = new(teamsRoot, Path.Combine(root, "Raw"), Path.Combine(root, "Archive"), workDir, prompts);
        string path = Path.Combine(teamsRoot, "Home", "raw", "r1", "a.pdf");
        PermissionRequestContext request = new(
            "session-1",
            new ToolCallInfo("call-1", "Tool", ToolKind.Delete, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}"""),
            BothKinds);

        WorkDirPermissionHandler guarded = new(Path.Combine(root, ".claude"), NullLogger<WorkDirPermissionHandler>.Instance, workDir: workDir, rawGuard: guard);
        WorkDirPermissionHandler unguarded = new(Path.Combine(root, ".claude"), NullLogger<WorkDirPermissionHandler>.Instance, workDir: workDir);

        PermissionDecision refused = await guarded.DecideAsync(request, TestContext.Current.CancellationToken);
        PermissionDecision approved = await unguarded.DecideAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(refused).OptionId);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(approved).OptionId);
    }
}
