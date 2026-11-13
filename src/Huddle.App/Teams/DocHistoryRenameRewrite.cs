using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Step-6 rewrite of the doc-history (Spec §4): the history of <c>teams/&lt;Old&gt;</c>, <c>archive/&lt;Old&gt;</c> and the workspace root, whose
/// id <c>workspace-{slug}</c> is computed from the Team name, moves to the new names. The same slug (a case-only rename) moves nothing.
/// </summary>
/// <param name="history">The doc-history store.</param>
internal sealed class DocHistoryRenameRewrite(DocHistoryStore history) : ITeamRenameRewrite
{
    /// <inheritdoc />
    public IReadOnlyList<string> TeamsTouched(TeamRename rename) => [];

    /// <inheritdoc />
    public Task RunAsync(TeamRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);

        LibraryRoot teams = new("teams", "Teams", string.Empty, LibraryRootKind.Teams);
        if (rename.Kind == TeamRenameKind.Project)
        {
            history.Move(Folder(teams, $"{rename.Team}/{rename.OldName}"), Folder(teams, $"{rename.Team}/{rename.NewName}"));
            return Task.CompletedTask;
        }

        LibraryRoot archive = new("archive", "Archive", string.Empty, LibraryRootKind.Archive, ReadOnly: true);
        history.Move(Folder(teams, rename.OldName), Folder(teams, rename.NewName));
        history.Move(Folder(archive, rename.OldName), Folder(archive, rename.NewName));

        string oldSlug = LibraryRootStore.Slug("workspace-" + rename.OldName, []);
        string newSlug = LibraryRootStore.Slug("workspace-" + rename.NewName, []);
        if (!string.Equals(oldSlug, newSlug, StringComparison.Ordinal))
        {
            history.Move(WorkspaceRoot(oldSlug), WorkspaceRoot(newSlug));
        }

        return Task.CompletedTask;
    }

    private static LibraryPath Folder(LibraryRoot root, string name) => new(root, name, name, LibraryNodeRole.Folder);

    private static LibraryPath WorkspaceRoot(string slug) =>
        new(new LibraryRoot(slug, slug, string.Empty, LibraryRootKind.Workspace), string.Empty, string.Empty, LibraryNodeRole.Root);
}
