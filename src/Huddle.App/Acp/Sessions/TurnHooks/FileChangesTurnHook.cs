using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;
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
internal sealed class FileChangesTurnHook(FileChangeTracker tracker, IPromptSource prompts, ILogger<FileChangesTurnHook> logger)
    : TurnHook<CollectedChanges>
{
    /// <inheritdoc />
    public override string Name => "FileChanges";

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

        return new TurnAddition(Render(collected.Report, prompts), collected);
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
