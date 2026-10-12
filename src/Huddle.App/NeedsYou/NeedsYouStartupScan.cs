using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.NeedsYou;

/// <summary>
/// Works out again, once, which Rooms hold an unanswered Mention of the Human, from the end of each
/// Transcript (Needs You spec §6.3). Questions, Forms and Budgets are in memory and empty after a
/// restart, so only the Mention reason needs rebuilding. Runs in the background so a large data folder
/// does not delay the window opening.
/// </summary>
internal sealed class NeedsYouStartupScan(
    NeedsYouTracker tracker,
    ITeamDirectory teamDirectory,
    IChatStore chatStore,
    IMentionAliasSource aliasSource,
    ILogger<NeedsYouStartupScan> logger) : BackgroundService
{
    /// <summary>How many Messages at the end of each Transcript the scan reads.</summary>
    internal const int TailLength = 50;

    /// <summary>Scans every Room, Archived ones included, and seeds the tracker for each unanswered Mention.</summary>
    /// <param name="stoppingToken">Stops the scan when the host shuts down.</param>
    /// <returns>A task that completes when every Room has been scanned.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        IReadOnlyList<Room> rooms;
        try
        {
            rooms = await teamDirectory.GetRoomsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Needs You could not list Rooms for its startup scan.");
            return;
        }

        foreach (Room room in rooms)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await this.ScanAsync(room.Id, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Needs You could not scan Room {RoomId} at startup.", room.Id);
            }
        }
    }

    /// <summary>Scans one Room's Transcript tail.</summary>
    /// <param name="roomId">The Room to scan.</param>
    /// <param name="ct">Cancels the reads.</param>
    /// <returns>A task that completes when the Room has been seeded or found clear.</returns>
    private async Task ScanAsync(string roomId, CancellationToken ct)
    {
        IReadOnlyList<User> members = await teamDirectory.GetRoomMembersAsync(roomId, ct);
        IReadOnlyList<ChatMessage> tail = await chatStore.ReadTailAsync(roomId, NeedsYouStartupScan.TailLength, ct);
        IReadOnlyList<MentionAlias> aliases = aliasSource.Aliases;

        ChatMessage? mention = NeedsYouRule.OldestUnansweredMention(
            tail,
            message => MentionParser.Parse(message.Text, members, aliases)
                .Any(user => string.Equals(user.Id, KnownIds.Human, StringComparison.Ordinal)));

        if (mention is not null)
        {
            tracker.Seed(roomId, mention.Timestamp, mention.SenderName);
        }
    }
}
