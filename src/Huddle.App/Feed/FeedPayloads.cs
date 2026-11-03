using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Agency.Huddle.App.Feed;

/// <summary>Pairs each <see cref="FeedSourceType"/> with its payload record and reads and writes the JSON envelope of spec 6.4.</summary>
internal static class FeedPayloads
{
    private const int Version = 1;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> NonNullableProperties = new();

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>The payload record each source type carries.</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The record type.</returns>
    public static Type TypeOf(FeedSourceType type) => type switch
    {
        FeedSourceType.TaskCreated => typeof(TaskCreatedPayload),
        FeedSourceType.TaskGateOn => typeof(TaskGatePayload),
        FeedSourceType.TaskNeedsYou => typeof(TaskNeedsYouPayload),
        FeedSourceType.TaskAssignedToYou => typeof(TaskAssignedPayload),
        FeedSourceType.RequestAwaitsYou => typeof(RequestAwaitsPayload),
        FeedSourceType.GateDecisionDue => typeof(GateDecisionPayload),
        FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.RoomJob => typeof(RoomWaitPayload),
        FeedSourceType.RoomCreated => typeof(RoomCreatedPayload),
        FeedSourceType.RoomJoined => typeof(RoomJoinedPayload),
        FeedSourceType.RoomArchived => typeof(RoomArchivedPayload),
        FeedSourceType.DocumentChanged => typeof(DocumentChangedPayload),
        FeedSourceType.HelperPaused => typeof(HelperPausedPayload),
        FeedSourceType.HelperFailing => typeof(HelperFailingPayload),
        FeedSourceType.TeamMemoryNotFiled => typeof(TeamMemoryNotFiledPayload),
        FeedSourceType.PairingRequested => typeof(PairingRequestedPayload),
        FeedSourceType.AdminRequested => typeof(AdminRequestedPayload),
        FeedSourceType.DevicePaired => typeof(DevicePairedPayload),
        FeedSourceType.DeviceRevoked => typeof(DeviceRevokedPayload),
        FeedSourceType.SecondBrainOverCap => typeof(SecondBrainOverCapPayload),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Whether a payload is the record its source type carries.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="payload">The payload.</param>
    /// <returns><see langword="true"/> when the payload's type is exactly <see cref="TypeOf"/>.</returns>
    public static bool Matches(FeedSourceType type, FeedPayload payload) => payload.GetType() == TypeOf(type);

    /// <summary>Writes the envelope <c>{ "v": 1, "actorKind": ..., "data": ... }</c>.</summary>
    /// <param name="actorKind">The actor's kind.</param>
    /// <param name="payload">The payload, serialised by its runtime type.</param>
    /// <returns>The JSON text for the <c>payload</c> column.</returns>
    public static string Serialize(FeedActorKind actorKind, FeedPayload payload)
    {
        JsonElement data = JsonSerializer.SerializeToElement(payload, payload.GetType(), Options);
        return JsonSerializer.Serialize(new Wire(Version, ActorKindToWire(actorKind), data), Options);
    }

    /// <summary>Reads the envelope. A row that cannot be read gives the parts that could be; it never throws.</summary>
    /// <param name="json">The column text.</param>
    /// <param name="type">The source type, or <see langword="null"/> when this build does not know the row's wire name.</param>
    /// <returns>The actor kind and payload that were readable.</returns>
    public static FeedEnvelope Read(string? json, FeedSourceType? type)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            Wire? wire = JsonSerializer.Deserialize<Wire>(json, Options);
            if (wire is null)
            {
                return default;
            }

            FeedPayload? payload = null;
            if (type is { } known && wire.Data.ValueKind == JsonValueKind.Object)
            {
                payload = wire.Data.Deserialize(TypeOf(known), Options) as FeedPayload;
                if (payload is not null && !HasRequiredMembers(payload))
                {
                    payload = null;
                }
            }

            return new FeedEnvelope(ActorKindFromWire(wire.ActorKind), payload);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// Whether every member a payload declares non-nullable was present in the JSON. A row from a newer build that renamed a field
    /// would otherwise deserialise with nulls the UI is entitled to assume away, so it reads as an unreadable item instead.
    /// </summary>
    /// <param name="payload">The deserialised payload.</param>
    private static bool HasRequiredMembers(FeedPayload payload) =>
        NonNullableProperties.GetOrAdd(payload.GetType(), static type => ListNonNullableProperties(type))
            .All(property => property.GetValue(payload) is not null);

    private static PropertyInfo[] ListNonNullableProperties(Type type)
    {
        NullabilityInfoContext context = new();
        return [.. type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !property.PropertyType.IsValueType && context.Create(property).ReadState == NullabilityState.NotNull)];
    }

    private static string ActorKindToWire(FeedActorKind kind) => kind switch
    {
        FeedActorKind.Human => "human",
        FeedActorKind.Agent => "agent",
        FeedActorKind.Huddle => "huddle",
        FeedActorKind.OutsideHuddle => "outside-huddle",
        FeedActorKind.Helper => "helper",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static FeedActorKind? ActorKindFromWire(string? wire) => wire switch
    {
        "human" => FeedActorKind.Human,
        "agent" => FeedActorKind.Agent,
        "huddle" => FeedActorKind.Huddle,
        "outside-huddle" => FeedActorKind.OutsideHuddle,
        "helper" => FeedActorKind.Helper,
        _ => null,
    };

    private sealed record Wire(int V, string? ActorKind, JsonElement Data);
}
