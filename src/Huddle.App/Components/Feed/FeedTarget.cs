using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>What a Feed item is about, and the address that opens it (Feed spec §6.12, §6.13).</summary>
/// <param name="Kind">Whether it is a Task, a Room, a Library document or a Helper.</param>
/// <param name="Id">The Task id, the Room id, the document's absolute path or the Helper id.</param>
internal sealed record FeedTarget(FeedTargetKind Kind, string Id)
{
    /// <summary>The target an item's payload names.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The target; an item this build cannot read has a target of no kind.</returns>
    public static FeedTarget Of(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Payload switch
        {
            TaskCreatedPayload p => new(FeedTargetKind.Task, p.Id),
            TaskGatePayload p => new(FeedTargetKind.Task, p.Id),
            TaskNeedsYouPayload p => new(FeedTargetKind.Task, p.Id),
            TaskAssignedPayload p => new(FeedTargetKind.Task, p.Id),
            RequestAwaitsPayload p => new(FeedTargetKind.Task, p.Id),
            GateDecisionPayload p => new(FeedTargetKind.Task, p.Id),
            RoomWaitPayload p => new(FeedTargetKind.Room, p.RoomId),
            RoomCreatedPayload p => new(FeedTargetKind.Room, p.RoomId),
            RoomJoinedPayload p => new(FeedTargetKind.Room, p.RoomId),
            RoomArchivedPayload p => new(FeedTargetKind.Room, p.RoomId),
            DocumentChangedPayload p => new(FeedTargetKind.Document, p.Path),
            HelperPausedPayload p => new(FeedTargetKind.Helper, p.HelperId),
            HelperFailingPayload p => new(FeedTargetKind.Helper, p.HelperId),
            PairingRequestedPayload p => new(FeedTargetKind.Device, p.RequestId),
            AdminRequestedPayload p => new(FeedTargetKind.Device, p.RequestId),
            DevicePairedPayload p => new(FeedTargetKind.Device, p.DeviceId),
            DeviceRevokedPayload p => new(FeedTargetKind.Device, p.DeviceId),
            _ => new(FeedTargetKind.None, string.Empty),
        };
    }

    /// <summary>The Library page address of a document, or <see langword="null"/> when its path is under no Library root.</summary>
    /// <param name="resolver">Resolves an absolute path to a Library path.</param>
    /// <param name="absolutePath">The document's absolute path.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    public static string? DocumentAddress(LibraryPathResolver resolver, string? absolutePath) =>
        absolutePath is not null && resolver.TryResolveAbsolute(absolutePath, out LibraryPath? path)
            ? LibraryLinkQuery.BuildPageUri(path)
            : null;

    /// <summary>The address of Settings > Remote access, where every device item leads.</summary>
    public const string DevicesAddress = "/settings/remoteaccess";

    /// <summary>The address of a Helper's card on Settings > Helpers.</summary>
    /// <param name="helperId">The Helper's id, such as <c>NeedsHuman</c>.</param>
    /// <returns>The address, with the anchor the card carries.</returns>
    public static string HelperAddress(string helperId) => "/settings/helpers#" + HelperAnchor(helperId);

    /// <summary>The element id a Helper's card carries on Settings > Helpers, which the address above names.</summary>
    /// <param name="helperId">The Helper's id, such as <c>NeedsHuman</c>.</param>
    /// <returns>The id, such as <c>helper-NeedsHuman</c>.</returns>
    public static string HelperAnchor(string helperId) => "helper-" + helperId;

    /// <summary>The address that opens this target.</summary>
    /// <param name="resolver">Resolves a document's path.</param>
    /// <returns>The address, or <see langword="null"/> when there is none.</returns>
    public string? Address(LibraryPathResolver resolver) => this.Kind switch
    {
        FeedTargetKind.Task => "/tasks/item/" + this.Id,
        FeedTargetKind.Room => "/rooms/" + this.Id,
        FeedTargetKind.Document => DocumentAddress(resolver, this.Id),
        FeedTargetKind.Helper => HelperAddress(this.Id),
        FeedTargetKind.Device => DevicesAddress,
        _ => null,
    };
}
