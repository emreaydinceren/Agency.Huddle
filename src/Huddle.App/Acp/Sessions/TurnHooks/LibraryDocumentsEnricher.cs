using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using System.Globalization;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Spec §6.14, corrections-B6 item 7 / B5 item 28: collects the Library documents the Turn's texts mention, after the
/// Transcript Catch-up read and before the locked check-and-publish.
/// </summary>
/// <remarks>
/// Collected before the publish so a slow scan is never counted as Adapter silence, the same reasoning the File Changes
/// collect already relies on. Scans the triggering Message's own text, the catch-up buffer and, when this Turn set one,
/// the Transcript Catch-up - never OwnPostLines (this Agent's own earlier posts, not new documents to fetch).
/// </remarks>
/// <param name="collector">Collects the documents and supplies the per-Turn caps.</param>
/// <param name="readsFiles">Whether the resolved Adapter Profile can read files (FC §6.11's Library counterpart): <see langword="false"/> inlines a kept document's own text instead of just its path.</param>
/// <param name="logger">Receives the Information line for an image that went as a path line instead of a block.</param>
internal sealed class LibraryDocumentsEnricher(LibraryDocumentCollector collector, bool readsFiles, ILogger logger) : ITurnEnricher
{
    /// <summary>The enricher's name, as the pipeline's production order knows it.</summary>
    internal const string EnricherName = "LibraryDocuments";

    /// <inheritdoc />
    public string Name => EnricherName;

    /// <inheritdoc />
    public async Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);
        WorkItem item = turn.Item;
        IReadOnlyList<string> texts =
        [
            item.Text,
            .. item.Folded?.Skip(1).Select(folded => folded.Text) ?? [],
            .. item.MissedMessages.Select(missed => missed.Text),
            .. item.Transcript?.Messages.Select(message => message.Text) ?? [],
        ];
        var report = await collector.CollectAsync(texts, this.PromptDeliveryFor(turn.Host), cancellationToken).ConfigureAwait(false);
        this.LogWithheldImages(report, turn.PersonaName, item.RoomId);
        return item with { LibraryDocuments = report };
    }

    /// <summary>
    /// What this Turn's prompt may carry beyond text, built from what the Adapter advertised at <c>initialize</c> and the
    /// profile's <c>PromptBlocks</c> kill switch. No host (every caller that predates resume) means nothing was advertised,
    /// so nothing is sent.
    /// </summary>
    /// <param name="host">The Persona's host, or <see langword="null"/> when there is none.</param>
    private PromptDelivery PromptDeliveryFor(IPersonaHost? host)
    {
        AgentPromptCapabilities capabilities = host?.PromptCapabilities ?? AgentPromptCapabilities.None;
        bool allowed = host?.Profile.PromptBlocks ?? false;
        return collector.DeliveryFor(
            readsFiles,
            images: capabilities.Image && allowed,
            embeddedText: capabilities.EmbeddedContext && allowed && !readsFiles);
    }

    /// <summary>Logs one Information line when an image that could have gone as a block did not: counts by reason, never the path's contents.</summary>
    private void LogWithheldImages(LibraryDocumentsReport report, string personaName, string roomId)
    {
        List<string> reasons =
        [
            .. report.Items
                .Where(document => document.Withheld != PromptBlockWithheld.None)
                .GroupBy(document => document.Withheld)
                .OrderBy(group => group.Key)
                .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key}={group.Count()}")),
        ];

        if (reasons.Count > 0 && logger.IsEnabled(LogLevel.Information))
        {
            string summary = string.Join(", ", reasons);
            logger.LogInformation(
                "Persona '{PersonaName}' sent library file paths in room {RoomId} as path lines instead of blocks: {Reasons}.",
                personaName,
                roomId,
                summary);
        }
    }
}
