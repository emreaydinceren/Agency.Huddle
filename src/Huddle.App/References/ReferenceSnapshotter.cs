using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>Builds a Message's <see cref="MessageReference"/> list from its text (spec §8.1, §8.2). A failure never blocks a post.</summary>
/// <param name="resolver">Looks the candidates up now.</param>
/// <param name="logger">Records a lookup failure once.</param>
internal sealed class ReferenceSnapshotter(ReferenceResolver resolver, ILogger<ReferenceSnapshotter> logger) : IReferenceSnapshotter
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageReference>?> SnapshotAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            IReadOnlyList<ReferenceCandidate> candidates = ReferenceScanner.Scan(text);
            if (candidates.Count == 0)
            {
                return null;
            }

            ResolvedSet resolved = await resolver.ResolveCurrentAsync(candidates, ct);
            List<MessageReference> references = [];
            foreach (ResolvedReference reference in resolved.Items)
            {
                MessageReference? mapped = ReferenceSnapshotter.Map(reference);
                if (mapped is not null)
                {
                    references.Add(mapped);
                }
            }

            return references.Count == 0 ? null : references;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not build the reference snapshot; the Message is posted without references.");
            return null;
        }
    }

    /// <summary>Maps one resolved reference to the chip the Message stores; a Room keeps its plain label so the chip never shows Markdown escapes.</summary>
    /// <param name="reference">The resolved reference.</param>
    /// <returns>The stored reference, or null for a kind that has no chip.</returns>
    private static MessageReference? Map(ResolvedReference reference) => reference switch
    {
        ResolvedTask task => new MessageReference(ReferenceKind.Task, task.Id.ToString(), task.Id.ToString(), task.Title, task.Status.ToWire()),
        ResolvedRoom room => new MessageReference(ReferenceKind.Room, room.Id, ReferenceText.RoomPlainLabelFor(room.Name, room.Team, room.IsTeamRoom)),
        ResolvedTeam team => new MessageReference(ReferenceKind.Team, team.Name, team.Name),
        ResolvedLibraryFile file => new MessageReference(ReferenceKind.LibraryFile, file.Path, file.FileName),
        _ => null,
    };
}
