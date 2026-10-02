using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// The real <see cref="IElicitationBridge"/> (elicitation bridge, decisions E-5 and E-6): shows an agent's
/// form to the Human as a card in the Turn's Room and waits for how it ends. A form the reader cannot show
/// faithfully is declined at once, with a warning and no card. Cancelling the request - a Stop, the Turn
/// ending, a shutdown, the bound running out - drops the card so the Room stops showing it and resolves
/// the request as cancelled; the cancellation callback never throws, because a throwing callback can end
/// the Room Session's consumer loop.
/// </summary>
/// <param name="store">Holds the cards this bridge adds.</param>
/// <param name="directory">Resolves the asker's current Name, fresh for each request.</param>
/// <param name="logger">Used to log a form that was declined as unsupported.</param>
internal sealed partial class RoomElicitationBridge(
    ElicitationStore store,
    ITeamDirectory directory,
    ILogger<RoomElicitationBridge> logger) : IElicitationBridge
{
    /// <inheritdoc/>
    public async Task<ElicitationResult> RequestAsync(ElicitationContext context, ElicitationRequest request, CancellationToken cancellationToken)
    {
        if (!ElicitationSchemaReader.TryRead(request, out ElicitationForm? form, out string? problem))
        {
            LogUnsupported(logger, context.AgentId, context.RoomId, problem);
            return new ElicitationDeclined();
        }

        string askerName;
        try
        {
            User? asker = await directory.GetUserAsync(context.AgentId, cancellationToken);
            askerName = asker?.Name ?? context.AgentId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ElicitationCancelled();
        }

        PendingElicitation card = store.Add(context.RoomId, context.AgentId, askerName, form);
        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            _ = store.DropOne(card.RoomId, card.Id);
            _ = card.Completion.TrySetResult(new ElicitationCancelled());
        });

        return await card.Completion.Task;
    }

    /// <summary>Logs that a form was declined because its schema is not one the bridge can show.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="agentId">The Agent that asked.</param>
    /// <param name="roomId">The Room of the Turn the form was asked in.</param>
    /// <param name="problem">Why the reader refused the schema.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Declined an unsupported form from agent {AgentId} in room {RoomId}: {Problem}")]
    private static partial void LogUnsupported(ILogger logger, string agentId, string roomId, string? problem);
}
