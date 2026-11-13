using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins <see cref="RenamedFolderGuard"/> (Team Rename R10): an Edit or Write under a Team's old folder is refused, and the guard sits in <see cref="PermissionGuardList"/>.</summary>
public sealed class RenamedFolderGuardTests : IDisposable
{
    private readonly TempDataDir dir = new();

    public void Dispose() => this.dir.Dispose();

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string WorkDir => Path.Combine(this.dir.Path, "work");

    private RenamedFolderGuard Guard()
    {
        TeamRenameStore renames = new(this.dir.Options());
        renames.Complete(renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        return new RenamedFolderGuard(this.TeamsRoot, this.WorkDir, renames);
    }

    private static ToolCallInfo Call(ToolKind kind, string path) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");

    /// <summary>An Edit or Write under the old Team folder is refused.</summary>
    [Fact]
    public void RefusalFor_EditUnderOldTeamFolder_IsRefused()
    {
        RenamedFolderGuard guard = this.Guard();

        Assert.NotNull(((IToolCallGuard)guard).RefusalFor(Call(ToolKind.Edit, Path.Combine(this.TeamsRoot, "Marketing", "docs", "plan.md")), "Nova"));
        Assert.NotNull(((IToolCallGuard)guard).RefusalFor(Call(ToolKind.Edit, Path.Combine(this.TeamsRoot, "marketing", "plan.md")), "Nova"));
    }

    /// <summary>The Team's new folder and a sibling that only starts with the old name are allowed.</summary>
    [Fact]
    public void RefusalFor_NewFolderAndSibling_AreAllowed()
    {
        RenamedFolderGuard guard = this.Guard();

        Assert.Null(((IToolCallGuard)guard).RefusalFor(Call(ToolKind.Edit, Path.Combine(this.TeamsRoot, "Growth", "plan.md")), "Nova"));
        Assert.Null(((IToolCallGuard)guard).RefusalFor(Call(ToolKind.Edit, Path.Combine(this.TeamsRoot, "Marketing2", "plan.md")), "Nova"));
    }

    /// <summary>The guard is consulted after Team Memory and before the Agent's settings guard.</summary>
    [Fact]
    public void InOrder_WithRenamedFolderGuard_PlacesItAfterTeamMemoryBeforeSettings()
    {
        string inert = Path.Combine(this.dir.Path, "Inert");
        FakePromptSource prompts = new();
        ProtectedDirectoryGuard protectedDirectory = new(Path.Combine(this.dir.Path, ".claude"), this.WorkDir);
        AgentsMdGuard agentsMd = new(inert, this.WorkDir, prompts);
        TeamMemoryWriteGuard teamMemory = new(inert, this.WorkDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts);
        RawGuard raw = new(this.TeamsRoot, Path.Combine(this.dir.Path, "Raw"), Path.Combine(this.dir.Path, "Archive"), this.WorkDir, prompts);
        PromptsRootGuard promptsGuard = new(Path.Combine(inert, "Prompts"), this.WorkDir, prompts);
        AgentSettingsGuard settings = new(this.WorkDir, Path.Combine(this.dir.Path, "hooks"));
        RenamedFolderGuard renamed = this.Guard();

        IReadOnlyList<IToolCallGuard> guards = PermissionGuardList.InOrder(
            agentsMd,
            teamMemory,
            raw,
            promptsGuard,
            settings,
            protectedDirectory,
            renamedFolder: renamed);

        Assert.Equal<IToolCallGuard>([protectedDirectory, agentsMd, raw, promptsGuard, teamMemory, renamed, settings], guards);
    }
}
