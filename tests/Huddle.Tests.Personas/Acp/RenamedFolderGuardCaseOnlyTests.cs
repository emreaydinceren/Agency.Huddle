using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins <see cref="RenamedFolderGuard"/> after a case-only rename (batch 3 review): the old spelling is refused ordinally, today's exact spelling is allowed.</summary>
public sealed class RenamedFolderGuardCaseOnlyTests : IDisposable
{
    private readonly TempDataDir dir = new();

    public void Dispose() => this.dir.Dispose();

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string? Refusal(string team)
    {
        TeamRenameStore renames = new(this.dir.Options());
        renames.Complete(renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "marketing"));
        RenamedFolderGuard guard = new(this.TeamsRoot, Path.Combine(this.dir.Path, "work"), renames);
        string path = Path.Combine(this.TeamsRoot, team, "x.md");
        ToolCallInfo call = new("call-1", "Write", ToolKind.Edit, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
        return ((IToolCallGuard)guard).RefusalFor(call, "Nova");
    }

    /// <summary>After Marketing became marketing, a Write under the old spelling Marketing is refused (on Linux it would make a second folder).</summary>
    [Fact]
    public void RefusalFor_OldSpellingAfterCaseOnlyRename_IsRefused() =>
        Assert.Equal("Team 'Marketing' was renamed to 'marketing'. Use 'marketing'.", this.Refusal("Marketing"));

    /// <summary>After the same rename, a Write under today's exact spelling is allowed.</summary>
    [Fact]
    public void RefusalFor_TodaysExactSpellingAfterCaseOnlyRename_IsAllowed() =>
        Assert.Null(this.Refusal("marketing"));
}
