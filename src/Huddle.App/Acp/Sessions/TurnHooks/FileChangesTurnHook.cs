using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Tells a Persona which files changed since its last Turn in a Room (Spec §6.10). Before collects the scan and renders
/// the block; After commits the scan plus the paths the Turn touched. Stateless: the scan travels as the carry.
/// </summary>
/// <param name="tracker">Collects and commits the File Changes.</param>
/// <param name="prompts">Supplies the block's wording.</param>
/// <param name="logger">Receives the warnings for a failed collect or commit.</param>
/// <param name="scopes">Says which <c>AGENTS.md</c> files apply to the Persona, so the AGENTS.md notice is not told twice; <see langword="null"/> filters nothing.</param>
internal sealed class FileChangesTurnHook(FileChangeTracker tracker, IPromptSource prompts, ILogger<FileChangesTurnHook> logger, AgentsMdScopes? scopes = null)
    : TurnHook<CollectedChanges>
{
    /// <summary>The hook's name, as the pipeline's production order and the session's hook state know it.</summary>
    internal const string HookName = "FileChanges";

    /// <inheritdoc />
    public override string Name => HookName;

    /// <inheritdoc />
    public override bool AppliesTo(TurnHookScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.FileChangesOn;
    }

    /// <inheritdoc />
    public override async Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        CollectedChanges collected;
        try
        {
            collected = await tracker
                .CollectAsync(turn.Scope.PersonaName, turn.RoomId, turn.Scope.DeclaredWatches, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Persona '{PersonaName}' failed to collect File Changes for room {RoomId}.", turn.Scope.PersonaName, turn.RoomId);
            return null;
        }

        return new TurnAddition(Render(this.WithoutInScopeAgentsMd(collected.Report, turn.Scope.PersonaName), prompts), collected);
    }

    /// <summary>
    /// Builds the report to render without the Persona's in-scope <c>AGENTS.md</c> paths (Spec 6.7). The carry keeps the
    /// original, so the baseline still records the file. A failure to list the scopes leaves the report whole.
    /// <c>NotListed</c> may still count capped AGENTS.md paths: it counts what the cap left out before this filter ran, and is not adjusted.
    /// </summary>
    private FileChangesReport? WithoutInScopeAgentsMd(FileChangesReport? report, string personaName)
    {
        if (scopes is null || report is null || report.Changes.Count == 0)
        {
            return report;
        }

        try
        {
            IReadOnlyList<AgentsMdFile> files = scopes.For(personaName);
            if (files.Count == 0)
            {
                return report;
            }

            StringComparer comparer = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            HashSet<string> inScope = new(files.Select(static file => file.FullPath), comparer);
            HashSet<string> teamDirs = new(files.Where(static file => file.Level == AgentsMdLevel.Team).Select(static file => Path.GetDirectoryName(file.FullPath) ?? string.Empty), comparer);
            List<FileChange> kept = [.. report.Changes.Where(change => !inScope.Contains(change.FullPath) && !IsLevelPath(change.FullPath, teamDirs))];
            return kept.Count == report.Changes.Count ? report : report with { Changes = kept };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Persona '{PersonaName}' could not list its AGENTS.md scopes; File Changes are not filtered.", personaName);
            return report;
        }
    }

    /// <summary>
    /// Whether a path is an <c>AGENTS.md</c> directly in a Team folder or in a Project folder directly under one: a level path, whether or not
    /// the level still exists. A Team's reserved folders hold no Project, so no notice covers a file there and it stays a File Change.
    /// </summary>
    private static bool IsLevelPath(string fullPath, HashSet<string> teamDirs)
    {
        if (!string.Equals(Path.GetFileName(fullPath), "AGENTS.md", PathContainment.Comparison))
        {
            return false;
        }

        string? folder = Path.GetDirectoryName(fullPath);
        return folder is not null
            && (teamDirs.Contains(folder)
                || (teamDirs.Contains(Path.GetDirectoryName(folder) ?? string.Empty) && !TeamNames.IsReservedProjectName(Path.GetFileName(folder))));
    }

    /// <inheritdoc />
    protected override async Task<string?> AfterTurnAsync(TurnEnded turn, CollectedChanges? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (carry is not null)
        {
            try
            {
                await tracker
                    .CommitAsync(turn.Scope.PersonaName, turn.RoomId, carry, new HashSet<string>(turn.Touched, StringComparer.Ordinal), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Persona '{PersonaName}' failed to commit File Changes for room {RoomId}.", turn.Scope.PersonaName, turn.RoomId);
            }
        }

        return turn.State;
    }

    /// <summary>
    /// Renders the File Changes block: the header, one line per changed file with a by-you suffix when the Persona made it,
    /// a line per unchecked folder, and the "…and N more." line when the cap left some out. Has no trailing newline.
    /// </summary>
    /// <param name="report">What changed, or null.</param>
    /// <param name="prompts">Supplies the wording.</param>
    /// <returns>The block, or null for a null or empty report.</returns>
    internal static string? Render(FileChangesReport? report, IPromptSource prompts)
    {
        if (report is null || report.IsEmpty)
        {
            return null;
        }

        StringBuilder builder = new();
        builder.Append(prompts.Render("turn.fileChangesHeader", new Dictionary<string, string>()));

        foreach (FileChange change in report.Changes)
        {
            string key = change.Kind switch
            {
                FileChangeKind.Added => "turn.fileAdded",
                FileChangeKind.Changed => "turn.fileChanged",
                FileChangeKind.Deleted => "turn.fileDeleted",
                _ => throw new InvalidOperationException($"Unknown File Change kind '{change.Kind}'."),
            };

            builder.Append('\n');
            builder.Append(prompts.Render(key, new Dictionary<string, string> { ["{{path}}"] = change.FullPath }));

            if (change.ByYouRoomName is not null)
            {
                builder.Append(prompts.Render("turn.fileByYouSuffix", new Dictionary<string, string> { ["{{roomName}}"] = change.ByYouRoomName }));
            }
        }

        foreach (string uncheckedFolder in report.Unchecked)
        {
            builder.Append('\n');
            builder.Append(prompts.Render(
                "turn.folderUnchecked",
                new Dictionary<string, string>
                {
                    ["{{path}}"] = uncheckedFolder,
                    ["{{max}}"] = report.MaxFilesPerFolder.ToString(CultureInfo.InvariantCulture),
                }));
        }

        if (report.NotListed > 0)
        {
            builder.Append('\n');
            builder.Append(prompts.Render(
                "turn.fileChangesMore",
                new Dictionary<string, string> { ["{{count}}"] = report.NotListed.ToString(CultureInfo.InvariantCulture) }));
        }

        return builder.ToString();
    }
}
