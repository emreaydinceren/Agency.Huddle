using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins the source types, their wire names, payload pairing, the envelope and the Category mapping (spec 6.3, 6.4, 6.9).</summary>
public sealed class FeedPayloadsTests
{
    private static readonly FeedSourceType[] AllTypes = Enum.GetValues<FeedSourceType>();

    /// <summary>Every source type has one non-empty wire name that parses back to it, and no two share a name.</summary>
    [Fact]
    public void WireName_EveryType_IsUniqueAndRoundTrips()
    {
        List<string> names = [];
        foreach (FeedSourceType type in AllTypes)
        {
            string wire = FeedSourceTypes.ToWire(type);
            Assert.False(string.IsNullOrWhiteSpace(wire));
            Assert.True(FeedSourceTypes.TryParse(wire, out FeedSourceType parsed));
            Assert.Equal(type, parsed);
            names.Add(wire);
        }

        Assert.Equal(AllTypes.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The wire names are the ones the spec fixes, because the column holds them forever.</summary>
    [Fact]
    public void WireName_Spec_Matches()
    {
        (FeedSourceType Type, string Wire)[] expected =
        [
            (FeedSourceType.TaskCreated, "task-created"),
            (FeedSourceType.TaskGateOn, "task-gate-on"),
            (FeedSourceType.TaskNeedsYou, "task-needs-you"),
            (FeedSourceType.TaskAssignedToYou, "task-assigned-to-you"),
            (FeedSourceType.RequestAwaitsYou, "request-awaits-you"),
            (FeedSourceType.GateDecisionDue, "gate-decision-due"),
            (FeedSourceType.RoomMention, "room-mention"),
            (FeedSourceType.RoomQuestion, "room-question"),
            (FeedSourceType.RoomForm, "room-form"),
            (FeedSourceType.RoomBudget, "room-budget"),
            (FeedSourceType.RoomJob, "room-job"),
            (FeedSourceType.RoomCreated, "room-created"),
            (FeedSourceType.RoomJoined, "room-joined"),
            (FeedSourceType.RoomArchived, "room-archived"),
            (FeedSourceType.DocumentChanged, "document-changed"),
            (FeedSourceType.HelperPaused, "helper-paused"),
            (FeedSourceType.HelperFailing, "helper-failing"),
            (FeedSourceType.TeamMemoryNotFiled, "team-memory-not-filed"),
            (FeedSourceType.AgentsMdChangeSkipped, "agents-md-change-skipped"),
            (FeedSourceType.PairingRequested, "pairing-requested"),
            (FeedSourceType.AdminRequested, "admin-requested"),
            (FeedSourceType.DevicePaired, "device-paired"),
            (FeedSourceType.DeviceRevoked, "device-revoked"),
            (FeedSourceType.SecondBrainOverCap, "second-brain-over-cap"),
            (FeedSourceType.TaskWakesPaused, "task-wakes-paused"),
            (FeedSourceType.TaskProceeded, "task-proceeded"),
            (FeedSourceType.SprintProceeded, "sprint-proceeded"),
            (FeedSourceType.SpendCapReached, "spend-cap-reached"),
            (FeedSourceType.TeamArchived, "team-archived"),
            (FeedSourceType.BuiltinAgentUpdateAvailable, "builtin-agent-update-available"),
            (FeedSourceType.AgentConnectorsChanged, "agent-connectors-changed"),
            (FeedSourceType.RoomMentionHeld, "room-mention-held"),
            (FeedSourceType.TeamRenameStuck, "team-rename-stuck"),
            (FeedSourceType.AgentBrowserChanged, "agent-browser-changed"),
        ];

        Assert.Equal(AllTypes.Length, expected.Length);
        Assert.All(expected, row => Assert.Equal(row.Wire, FeedSourceTypes.ToWire(row.Type)));
    }

    /// <summary>A wire name this build does not know does not parse.</summary>
    [Fact]
    public void TryParse_UnknownName_ReturnsFalse() =>
        Assert.False(FeedSourceTypes.TryParse("from-the-future", out _));

    /// <summary>Every source type names a payload type, and the sample payload of that type matches while another type's does not.</summary>
    [Fact]
    public void TypeOf_EveryType_NamesThePayloadAndMatchesOnlyIt()
    {
        foreach (FeedSourceType type in AllTypes)
        {
            Type payloadType = FeedPayloads.TypeOf(type);
            Assert.True(payloadType.IsSubclassOf(typeof(FeedPayload)));
            FeedPayload sample = FeedTestSupport.SamplePayload(type);
            Assert.IsType(payloadType, sample);
            Assert.True(FeedPayloads.Matches(type, sample));
        }

        Assert.False(FeedPayloads.Matches(FeedSourceType.RoomArchived, FeedTestSupport.SamplePayload(FeedSourceType.TaskCreated)));
    }

    /// <summary>Each payload survives the envelope, and the actor kind with it.</summary>
    [Fact]
    public void Envelope_EveryPayload_RoundTrips()
    {
        foreach (FeedSourceType type in AllTypes)
        {
            FeedPayload original = FeedTestSupport.SamplePayload(type);

            string json = FeedPayloads.Serialize(FeedActorKind.OutsideHuddle, original);
            FeedEnvelope read = FeedPayloads.Read(json, type);

            Assert.Equal(FeedActorKind.OutsideHuddle, read.ActorKind);
            Assert.NotNull(read.Payload);
            Assert.Equal(json, FeedPayloads.Serialize(FeedActorKind.OutsideHuddle, read.Payload));
        }
    }

    /// <summary>The AGENTS.md skip payload keeps its Helper id, name, path and reason through the envelope.</summary>
    [Fact]
    public void Envelope_AgentsMdChangeSkipped_RoundTripsEveryField()
    {
        AgentsMdChangeSkippedPayload original = new("AgentsMdChanges", "AGENTS.md Changes", "Teams/Home/AGENTS.md", "Fixes a spelling mistake");

        string json = FeedPayloads.Serialize(FeedActorKind.OutsideHuddle, original);
        FeedEnvelope read = FeedPayloads.Read(json, FeedSourceType.AgentsMdChangeSkipped);

        AgentsMdChangeSkippedPayload round = Assert.IsType<AgentsMdChangeSkippedPayload>(read.Payload);
        Assert.Equal(original, round);
    }

    /// <summary>The envelope is camelCase with a version and a data object, as the spec shows.</summary>
    [Fact]
    public void Serialize_TaskPayload_HasTheSpecShape()
    {
        string json = FeedPayloads.Serialize(FeedActorKind.Agent, new TaskCreatedPayload("GRO-41", "Draft the pricing page", "Growth", null));

        Assert.Contains("\"v\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"actorKind\":\"agent\"", json, StringComparison.Ordinal);
        Assert.Contains("\"data\":{\"id\":\"GRO-41\"", json, StringComparison.Ordinal);
        Assert.Contains("\"team\":\"Growth\"", json, StringComparison.Ordinal);
    }

    /// <summary>#353: a "wake-ups paused" payload saved before it carried a Room id still reads, with no Room; one that has it reads it back.</summary>
    [Fact]
    public void Read_TaskWakesPausedWithoutRoomId_StillReads()
    {
        string withRoom = FeedPayloads.Serialize(FeedActorKind.Huddle, new TaskWakesPausedPayload("PLAT-0001", "T", "Platform", "Nova", "room-1"));
        string old = withRoom.Replace(",\"roomId\":\"room-1\"", string.Empty, StringComparison.Ordinal);

        TaskWakesPausedPayload current = Assert.IsType<TaskWakesPausedPayload>(FeedPayloads.Read(withRoom, FeedSourceType.TaskWakesPaused).Payload);
        TaskWakesPausedPayload saved = Assert.IsType<TaskWakesPausedPayload>(FeedPayloads.Read(old, FeedSourceType.TaskWakesPaused).Payload);

        Assert.DoesNotContain("roomId", old, StringComparison.Ordinal);
        Assert.Equal("room-1", current.RoomId);
        Assert.Equal("Nova", saved.Waiting);
        Assert.Null(saved.RoomId);
    }

    /// <summary>Bad JSON, a data object of the wrong shape and an unknown type all read as a null payload without throwing.</summary>
    [Fact]
    public void Read_Malformed_GivesNullPayloadNeverThrows()
    {
        Assert.Null(FeedPayloads.Read("not json", FeedSourceType.TaskCreated).Payload);
        Assert.Null(FeedPayloads.Read(string.Empty, FeedSourceType.TaskCreated).Payload);
        Assert.Null(FeedPayloads.Read("{\"v\":1,\"actorKind\":\"agent\",\"data\":\"text\"}", FeedSourceType.TaskCreated).Payload);
        Assert.Null(FeedPayloads.Read("{\"v\":1,\"actorKind\":\"agent\"}", FeedSourceType.TaskCreated).Payload);

        string valid = FeedPayloads.Serialize(FeedActorKind.Agent, FeedTestSupport.SamplePayload(FeedSourceType.TaskCreated));
        FeedEnvelope unknownType = FeedPayloads.Read(valid, null);
        Assert.Null(unknownType.Payload);
        Assert.Equal(FeedActorKind.Agent, unknownType.ActorKind);
    }

    /// <summary>A data object that lacks a required field (a renamed field from a newer build) reads as an unreadable item, not as a payload with nulls.</summary>
    [Fact]
    public void Read_MissingRequiredField_GivesNullPayload()
    {
        FeedEnvelope missingTeam = FeedPayloads.Read("{\"v\":1,\"actorKind\":\"agent\",\"data\":{\"id\":\"GRO-1\",\"title\":\"T\"}}", FeedSourceType.TaskCreated);
        FeedEnvelope complete = FeedPayloads.Read("{\"v\":1,\"actorKind\":\"agent\",\"data\":{\"id\":\"GRO-1\",\"title\":\"T\",\"team\":\"Growth\"}}", FeedSourceType.TaskCreated);

        Assert.Null(missingTeam.Payload);
        Assert.Equal(FeedActorKind.Agent, missingTeam.ActorKind);
        Assert.IsType<TaskCreatedPayload>(complete.Payload);
    }

    /// <summary>The Helper actor kind is written as <c>helper</c>, and a row written before the kind existed still reads as it did.</summary>
    [Fact]
    public void ActorKind_Helper_IsWrittenAsHelper()
    {
        string json = FeedPayloads.Serialize(FeedActorKind.Helper, FeedTestSupport.SamplePayload(FeedSourceType.TaskGateOn));

        Assert.Contains("\"actorKind\":\"helper\"", json, StringComparison.Ordinal);
        Assert.Equal(FeedActorKind.Helper, FeedPayloads.Read(json, FeedSourceType.TaskGateOn).ActorKind);
        Assert.Equal(FeedActorKind.Agent, FeedPayloads.Read("{\"v\":1,\"actorKind\":\"agent\",\"data\":{}}", FeedSourceType.TaskGateOn).ActorKind);
    }

    /// <summary>A gate-on row stored before the Reason field existed reads with a null Reason, and one with a Reason keeps it.</summary>
    [Fact]
    public void Read_TaskGateRow_WithoutReason_ReadsNullReason()
    {
        const string Old = "{\"v\":1,\"actorKind\":\"agent\",\"data\":{\"id\":\"GRO-1\",\"title\":\"T\",\"team\":\"Growth\",\"action\":null}}";
        string withReason = FeedPayloads.Serialize(FeedActorKind.Helper, new TaskGatePayload("GRO-1", "T", "Growth", null, "Sends a document outside the Team"));

        TaskGatePayload old = Assert.IsType<TaskGatePayload>(FeedPayloads.Read(Old, FeedSourceType.TaskGateOn).Payload);
        TaskGatePayload fresh = Assert.IsType<TaskGatePayload>(FeedPayloads.Read(withReason, FeedSourceType.TaskGateOn).Payload);

        Assert.Null(old.Reason);
        Assert.Equal("Sends a document outside the Team", fresh.Reason);
    }

    /// <summary>Every actor kind has its own wire name and survives the envelope.</summary>
    [Fact]
    public void ActorKind_EveryKind_RoundTrips()
    {
        foreach (FeedActorKind kind in Enum.GetValues<FeedActorKind>())
        {
            string json = FeedPayloads.Serialize(kind, FeedTestSupport.SamplePayload(FeedSourceType.RoomArchived));
            Assert.Equal(kind, FeedPayloads.Read(json, FeedSourceType.RoomArchived).ActorKind);
        }
    }

    /// <summary>Every source type is in exactly one of Tasks, Gates, Rooms, Documents or Helpers, and All and Needs you list no types.</summary>
    [Fact]
    public void Categories_EveryType_IsInExactlyOneTypeSegment()
    {
        FeedCategory[] segments = [FeedCategory.Tasks, FeedCategory.Gates, FeedCategory.Rooms, FeedCategory.Documents, FeedCategory.Helpers, FeedCategory.Devices, FeedCategory.SecondBrain];
        foreach (FeedSourceType type in AllTypes)
        {
            int hits = segments.Count(segment => FeedCategories.SourceTypes(segment).Contains(type));
            Assert.Equal(1, hits);
            Assert.Contains(type, FeedCategories.SourceTypes(FeedCategories.Of(type)));
        }

        Assert.Empty(FeedCategories.SourceTypes(FeedCategory.All));
        Assert.Empty(FeedCategories.SourceTypes(FeedCategory.NeedsYou));
    }

    /// <summary>The table of spec 6.9 puts these types in these segments.</summary>
    [Fact]
    public void Categories_Spec_Table()
    {
        (FeedSourceType Type, FeedCategory Category)[] expected =
        [
            (FeedSourceType.TaskCreated, FeedCategory.Tasks),
            (FeedSourceType.TaskAssignedToYou, FeedCategory.Tasks),
            (FeedSourceType.RequestAwaitsYou, FeedCategory.Tasks),
            (FeedSourceType.TaskGateOn, FeedCategory.Gates),
            (FeedSourceType.TaskNeedsYou, FeedCategory.Gates),
            (FeedSourceType.GateDecisionDue, FeedCategory.Gates),
            (FeedSourceType.RoomBudget, FeedCategory.Rooms),
            (FeedSourceType.RoomJob, FeedCategory.Rooms),
            (FeedSourceType.RoomArchived, FeedCategory.Rooms),
            (FeedSourceType.DocumentChanged, FeedCategory.Documents),
            (FeedSourceType.HelperPaused, FeedCategory.Helpers),
            (FeedSourceType.HelperFailing, FeedCategory.Helpers),
            (FeedSourceType.TeamMemoryNotFiled, FeedCategory.Helpers),
            (FeedSourceType.AgentsMdChangeSkipped, FeedCategory.Helpers),
            (FeedSourceType.SecondBrainOverCap, FeedCategory.SecondBrain),
            (FeedSourceType.TaskWakesPaused, FeedCategory.Tasks),
            (FeedSourceType.TaskProceeded, FeedCategory.Tasks),
            (FeedSourceType.SprintProceeded, FeedCategory.Tasks),
            (FeedSourceType.SpendCapReached, FeedCategory.SecondBrain),
            (FeedSourceType.TeamArchived, FeedCategory.SecondBrain),
            (FeedSourceType.BuiltinAgentUpdateAvailable, FeedCategory.SecondBrain),
            (FeedSourceType.AgentConnectorsChanged, FeedCategory.SecondBrain),
            (FeedSourceType.AgentBrowserChanged, FeedCategory.SecondBrain),
        ];

        Assert.All(expected, row => Assert.Equal(row.Category, FeedCategories.Of(row.Type)));
    }

    /// <summary>The query-string values of the Category control parse, and anything else reads as All.</summary>
    [Fact]
    public void Categories_Parse_UnknownIsAll()
    {
        (string? Text, FeedCategory Category)[] expected =
        [
            ("needs-you", FeedCategory.NeedsYou),
            ("tasks", FeedCategory.Tasks),
            ("documents", FeedCategory.Documents),
            ("rooms", FeedCategory.Rooms),
            ("gates", FeedCategory.Gates),
            ("helpers", FeedCategory.Helpers),
            ("bogus", FeedCategory.All),
            (null, FeedCategory.All),
        ];

        Assert.All(expected, row => Assert.Equal(row.Category, FeedCategories.Parse(row.Text)));
    }

    /// <summary>Every Category has a <c>category=</c> value that parses back to it.</summary>
    [Fact]
    public void Categories_ToWire_RoundTripsThroughParse()
    {
        Assert.All(Enum.GetValues<FeedCategory>(), category => Assert.Equal(category, FeedCategories.Parse(FeedCategories.ToWire(category))));
        Assert.Equal("needs-you", FeedCategories.ToWire(FeedCategory.NeedsYou));
        Assert.Equal("all", FeedCategories.ToWire(FeedCategory.All));
    }
}
