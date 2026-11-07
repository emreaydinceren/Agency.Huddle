using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Pins the one "role covers verify" helper that the request confirmers and the Stop hook install share (#354).</summary>
public sealed class RoleCoversVerifyTests
{
    /// <summary>A role whose coverage lists verify counts; a role that covers nothing, an unknown role and no role do not.</summary>
    [Fact]
    public void CoversVerify_OnlyARoleThatCoversVerify()
    {
        using TempDataDir dir = new();
        ProcessGateService gates = TaskGateText.BuildGates(dir, null);

        Assert.True(gates.CoversVerify(new RoleRef("web", "qa", "1.2.0")));
        Assert.False(gates.CoversVerify(new RoleRef("web", "lead", "1.2.0")));
        Assert.False(gates.CoversVerify(new RoleRef("web", "nobody", "1.2.0")));
        Assert.False(gates.CoversVerify(new RoleRef("elsewhere", "qa", "1.0.0")));
        Assert.False(gates.CoversVerify(null));
    }

    /// <summary>The module's install gives the read reminder to an identity whose role covers verify, and to no other: a role that covers nothing, no role, no identity and no gate service all leave the flag out of the installed hook.</summary>
    /// <param name="roleId">The role's id in the <c>web</c> type, or an empty string for an identity with no role.</param>
    /// <param name="expected">Whether the installed hook carries the flag.</param>
    [Theory]
    [InlineData("qa", true)]
    [InlineData("lead", false)]
    [InlineData("", false)]
    public void InstallDocHistoryHook_FlagFollowsTheRole(string roleId, bool expected)
    {
        using TempDataDir dir = new();
        ProcessGateService gates = TaskGateText.BuildGates(dir, null);
        RoleRef? role = string.IsNullOrEmpty(roleId) ? null : new RoleRef("web", roleId, "1.2.0");
        PersonaIdentity identity = new("Ana", "Tester", "ana", [], Role: role);
        string workDir = Path.Combine(dir.Path, "work");

        DocHistoryModule.InstallStopHook(workDir, dir.Path, ["mcp__team__log_change"], identity, gates, NullLogger.Instance);

        Assert.Equal(expected, HookCommand(workDir).Contains("--remind-reads", StringComparison.Ordinal));
    }

    /// <summary>With no identity at all, or no gate service, nothing can say the role covers verify, so the hook is installed without the flag rather than not at all.</summary>
    [Fact]
    public void InstallDocHistoryHook_NoIdentityOrNoGates_InstallsWithoutTheFlag()
    {
        using TempDataDir dir = new();
        ProcessGateService gates = TaskGateText.BuildGates(dir, null);
        string first = Path.Combine(dir.Path, "first");
        string second = Path.Combine(dir.Path, "second");
        PersonaIdentity qa = new("Ana", "Tester", "ana", [], Role: new RoleRef("web", "qa", "1.2.0"));

        DocHistoryModule.InstallStopHook(first, dir.Path, ["mcp__team__log_change"], null, gates, NullLogger.Instance);
        DocHistoryModule.InstallStopHook(second, dir.Path, ["mcp__team__log_change"], qa, null, NullLogger.Instance);

        Assert.DoesNotContain("--remind-reads", HookCommand(first), StringComparison.Ordinal);
        Assert.DoesNotContain("--remind-reads", HookCommand(second), StringComparison.Ordinal);
    }

    /// <summary>The Stop hook's command line as installed in a Work Dir.</summary>
    /// <param name="workDir">The Work Dir.</param>
    private static string HookCommand(string workDir) =>
        File.ReadAllText(Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile));
}
