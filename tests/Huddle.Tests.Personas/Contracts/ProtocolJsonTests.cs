using System.Globalization;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

public sealed class ProtocolJsonTests
{
    [Fact]
    public void Serialize_UsesCamelCase_AndStringEnums()
    {
        var post = new PostMessage("room-1", "msg-1", "hi");
        var postJson = ProtocolJson.Serialize(post);
        Assert.Contains("\"roomId\"", postJson, StringComparison.Ordinal);

        var member = new MemberInfo("agent-1", "echo", UserKind.Agent);
        var welcome = new Welcome("agent-1", "echo", new List<RoomInfo> { new("room-1", "echo", new List<MemberInfo> { member }) });
        var welcomeJson = ProtocolJson.Serialize(welcome);

        Assert.Contains("\"kind\":\"agent\"", welcomeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Kind\"", welcomeJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_TypePropertyNotFirst_Succeeds()
    {
        var version = ProtocolVersion.Current.ToString(CultureInfo.InvariantCulture);
        var result = ProtocolJson.Deserialize("{\"name\":\"x\",\"type\":\"hello\",\"version\":" + version + "}");

        var hello = Assert.IsType<Hello>(result);
        Assert.Equal("x", hello.Name);
    }

    [Fact]
    public void Serialize_OmitsNulls()
    {
        var json = ProtocolJson.Serialize(new Hello("x", null));

        Assert.DoesNotContain("description", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MessagePosted_RoundTrips_Members()
    {
        var chatMessage = new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi @echo");
        var mentioned = new MemberInfo("agent-1", "echo", UserKind.Agent);
        var human = new MemberInfo("human", "You", UserKind.Human);
        var posted = new MessagePosted(
            "room-1", "echo", chatMessage, true, new List<MemberInfo> { mentioned }, new List<MemberInfo> { human, mentioned },
            AgentMessagesSinceHuman: 3, Budget: 40);

        var json = ProtocolJson.Serialize(posted);
        var result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.Id == "human" && m.Kind == UserKind.Human);
        Assert.Contains(result.Members, m => m.Id == "agent-1" && m.Kind == UserKind.Agent);
        Assert.Equal(3, result.AgentMessagesSinceHuman);
        Assert.Equal(40, result.Budget);
    }

    /// <summary>
    /// A <see cref="RoomInfo"/> for a Room that has Messages serialises <c>isEmpty</c> as a literal
    /// <c>true</c> on the wire, per Spec §7.2.
    /// </summary>
    [Fact]
    public void Welcome_RoomInfoIsEmpty_SerialisesAsLiteralJson()
    {
        MemberInfo member = new("agent-1", "echo", UserKind.Agent);
        RoomInfo room = new("room-1", "echo", new List<MemberInfo> { member }, IsEmpty: true);
        Welcome welcome = new("agent-1", "echo", new List<RoomInfo> { room });

        string json = ProtocolJson.Serialize(welcome);

        Assert.Contains("\"isEmpty\":true", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <see cref="Welcome"/> line with no <c>isEmpty</c> field — as an older server would send —
    /// deserialises <see cref="RoomInfo.IsEmpty"/> as <see langword="false"/>, so an older server can
    /// never cause a Greeting.
    /// </summary>
    [Fact]
    public void Welcome_IsEmptyAbsent_DeserialisesFalse()
    {
        string json = """
            {"type":"welcome","version":3,"agentId":"agent-1","name":"echo",
             "rooms":[{"id":"room-1","name":"echo","members":[]}]}
            """.ReplaceLineEndings(string.Empty);

        Welcome result = Assert.IsType<Welcome>(ProtocolJson.Deserialize(json));

        Assert.False(result.Rooms[0].IsEmpty);
    }

    /// <summary>
    /// Adding <see cref="RoomInfo.IsEmpty"/> is additive on a server-to-client record, so it costs no
    /// <see cref="ProtocolVersion"/> bump.
    /// </summary>
    [Fact]
    public void ProtocolVersion_StillThree()
    {
        Assert.Equal(3, ProtocolVersion.Current);
    }

    // The reason a new field on a server-to-client record costs no ProtocolVersion bump: a client
    // built before it still parses the line and ignores what it does not know. traps.md records the
    // opposite case - renaming a property silently changes the protocol with no signal at all.
    [Fact]
    public void MessagePosted_WithUnknownProperties_StillDeserialises()
    {
        var json = """
            {"type":"messagePosted","version":3,"roomId":"room-1","roomName":"echo",
             "message":{"id":"m1","timestamp":"2026-09-12T00:00:00+00:00","senderId":"human","senderName":"You","text":"hi"},
             "mentioned":false,"mentions":[],"members":[],
             "agentMessagesSinceHuman":2,"budget":40,"somethingFromALaterVersion":"ignored"}
            """.ReplaceLineEndings(string.Empty);

        var result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(2, result.AgentMessagesSinceHuman);
        Assert.Equal(40, result.Budget);
    }

    /// <summary>A V3 <see cref="ToolActivity"/> pins the exact wire shape and round-trips through <see cref="ProtocolJson"/>.</summary>
    [Fact]
    public void ProtocolJson_RoundTripsToolActivity()
    {
        ToolActivity activity = new("room-1", "m-7", "tc-1", "Reading file.cs", ToolActivityStatus.InProgress);

        string json = ProtocolJson.Serialize(activity);

        Assert.Equal(
            "{\"type\":\"toolActivity\",\"roomId\":\"room-1\",\"messageId\":\"m-7\",\"toolCallId\":\"tc-1\"," +
            "\"title\":\"Reading file.cs\",\"status\":\"inProgress\",\"version\":3}",
            json);

        ToolActivity result = Assert.IsType<ToolActivity>(ProtocolJson.Deserialize(json));
        Assert.Equal(activity, result);
    }

    /// <summary>An activity that carries none of the Turn detail members serialises exactly as a version 3 activity always did, so an older reader sees no change.</summary>
    [Fact]
    public void ToolActivity_WithoutNewFields_SerialisesExactlyAsBefore()
    {
        ToolActivity activity = new("room-1", "m-7", "tc-1", "Reading file.cs", ToolActivityStatus.InProgress);

        string json = ProtocolJson.Serialize(activity);

        Assert.Equal(
            "{\"type\":\"toolActivity\",\"roomId\":\"room-1\",\"messageId\":\"m-7\",\"toolCallId\":\"tc-1\"," +
            "\"title\":\"Reading file.cs\",\"status\":\"inProgress\",\"version\":3}",
            json);
    }

    /// <summary>An activity with a path, a line and an Edit pins the exact wire shape.</summary>
    [Fact]
    public void ToolActivity_WithEdit_SerialisesToLiteralJson()
    {
        ToolActivity activity = new(
            "room-1", "m-7", "tc-1", "Edit notes.md", ToolActivityStatus.Completed, "E:\\Data\\notes.md", 1, new EditChange("one", "two"));

        string json = ProtocolJson.Serialize(activity);

        Assert.Equal(
            "{\"type\":\"toolActivity\",\"roomId\":\"room-1\",\"messageId\":\"m-7\",\"toolCallId\":\"tc-1\"," +
            "\"title\":\"Edit notes.md\",\"status\":\"completed\",\"path\":\"E:\\\\Data\\\\notes.md\",\"line\":1," +
            "\"edit\":{\"oldText\":\"one\",\"newText\":\"two\",\"truncated\":false,\"omittedChanges\":0},\"version\":3}",
            json);
    }

    /// <summary>An activity with arguments and a result pins the exact wire shape, after the existing members, and round-trips.</summary>
    [Fact]
    public void ToolActivity_WithArgumentsAndResult_SerialisesToLiteralJsonAndRoundTrips()
    {
        ToolActivity activity = new(
            "room-1", "m-7", "tc-1", "mcp__team__add_evidence", ToolActivityStatus.Completed, Arguments: "{\"a\":1}", Result: "Nothing was added.");

        string json = ProtocolJson.Serialize(activity);

        Assert.Equal(
            "{\"type\":\"toolActivity\",\"roomId\":\"room-1\",\"messageId\":\"m-7\",\"toolCallId\":\"tc-1\"," +
            "\"title\":\"mcp__team__add_evidence\",\"status\":\"completed\",\"arguments\":\"{\\u0022a\\u0022:1}\"," +
            "\"result\":\"Nothing was added.\",\"version\":3}",
            json);
        Assert.Equal(activity, Assert.IsType<ToolActivity>(ProtocolJson.Deserialize(json)));
    }

    /// <summary>An activity with an Edit, including a new-file edit with no old side, round-trips unchanged.</summary>
    [Fact]
    public void ToolActivity_WithEdit_RoundTrips()
    {
        ToolActivity activity = new(
            "room-1", "m-7", "tc-1", "Write a.txt", ToolActivityStatus.InProgress, "a.txt", null, new EditChange(null, "hello", true, 2));

        ToolActivity result = Assert.IsType<ToolActivity>(ProtocolJson.Deserialize(ProtocolJson.Serialize(activity)));

        Assert.Equal(activity, result);
    }

    /// <summary>A line from a newer runner with a member this reader does not know still deserialises, so a rolling upgrade never drops an activity.</summary>
    [Fact]
    public void ToolActivity_WithAnUnknownProperty_StillDeserialises()
    {
        string json =
            "{\"type\":\"toolActivity\",\"roomId\":\"room-1\",\"messageId\":\"m-7\",\"toolCallId\":\"tc-1\"," +
            "\"title\":\"Edit x\",\"status\":\"pending\",\"kind\":\"edit\",\"version\":3}";

        ToolActivity result = Assert.IsType<ToolActivity>(ProtocolJson.Deserialize(json));

        Assert.Equal(new ToolActivity("room-1", "m-7", "tc-1", "Edit x", ToolActivityStatus.Pending), result);
    }

    /// <summary>A V3 <see cref="StopTurn"/> pins the exact wire shape and round-trips through <see cref="ProtocolJson"/>.</summary>
    [Fact]
    public void ProtocolJson_RoundTripsStopTurn()
    {
        StopTurn stopTurn = new("room-1");

        string json = ProtocolJson.Serialize(stopTurn);

        Assert.Equal("{\"type\":\"stopTurn\",\"roomId\":\"room-1\",\"version\":3}", json);

        StopTurn result = Assert.IsType<StopTurn>(ProtocolJson.Deserialize(json));
        Assert.Equal(stopTurn, result);
    }

    /// <summary>A <c>StartAfresh</c> (Spec §6.1, correction C2) pins the exact wire shape and round-trips through <see cref="ProtocolJson"/>.</summary>
    [Fact]
    public void ProtocolJson_RoundTripsStartAfresh()
    {
        Agency.Huddle.Contracts.StartAfresh startAfresh = new("room-1");

        string json = ProtocolJson.Serialize(startAfresh);

        Assert.Equal("{\"type\":\"startAfresh\",\"roomId\":\"room-1\",\"version\":3}", json);

        Agency.Huddle.Contracts.StartAfresh result = Assert.IsType<Agency.Huddle.Contracts.StartAfresh>(ProtocolJson.Deserialize(json));
        Assert.Equal(startAfresh, result);
    }

    /// <summary>A <c>FreshStartNote</c> (Spec §6.3, correction C2) pins the exact wire shape and round-trips through <see cref="ProtocolJson"/>.</summary>
    [Fact]
    public void ProtocolJson_RoundTripsFreshStartNote()
    {
        Agency.Huddle.Contracts.FreshStartNote note = new("room-1", "asked", 38, 31000, false);

        string json = ProtocolJson.Serialize(note);

        Assert.Equal("{\"type\":\"freshStartNote\",\"roomId\":\"room-1\",\"reason\":\"asked\",\"messageCount\":38,\"estimatedTokens\":31000,\"readFailed\":false,\"version\":3}", json);

        Agency.Huddle.Contracts.FreshStartNote result = Assert.IsType<Agency.Huddle.Contracts.FreshStartNote>(ProtocolJson.Deserialize(json));
        Assert.Equal(note, result);
    }

    /// <summary>A hand-written line carrying the retired V2 version number is rejected by the strict version check.</summary>
    [Fact]
    public void ProtocolJson_RejectsAVersionTwoLine()
    {
        const string json = "{\"type\":\"hello\",\"version\":2,\"name\":\"x\"}";

        Assert.Throws<JsonException>(() => ProtocolJson.Deserialize(json));
    }

    /// <summary>
    /// A <see cref="ReadTranscript"/> with no <see cref="ReadTranscript.AfterMessageId"/> pins the
    /// exact wire shape (RS §6.5): no <c>afterMessageId</c> key at all, since
    /// <see cref="ProtocolJson.Options"/> sets <c>DefaultIgnoreCondition = WhenWritingNull</c>
    /// (correction item 24: property order, version last, matches what <see cref="ProtocolJson"/>
    /// really emits).
    /// </summary>
    [Fact]
    public void ReadTranscript_SerialisesToLiteralJson()
    {
        ReadTranscript read = new("r1", "room1", null, "m9", 20);

        string json = ProtocolJson.Serialize(read);

        Assert.Equal(
            "{\"type\":\"readTranscript\",\"requestId\":\"r1\",\"roomId\":\"room1\",\"beforeMessageId\":\"m9\",\"max\":20,\"version\":3}",
            json);

        ReadTranscript result = Assert.IsType<ReadTranscript>(ProtocolJson.Deserialize(json));
        Assert.Equal(read, result);
    }

    /// <summary>A <see cref="ReadTranscript"/> with <see cref="ReadTranscript.AfterMessageId"/> set carries it on the wire, between <c>roomId</c> and <c>beforeMessageId</c>.</summary>
    [Fact]
    public void ReadTranscript_WithAfter_SerialisesIt()
    {
        ReadTranscript read = new("r1", "room1", "m2", "m9", 20);

        string json = ProtocolJson.Serialize(read);

        Assert.Equal(
            "{\"type\":\"readTranscript\",\"requestId\":\"r1\",\"roomId\":\"room1\",\"afterMessageId\":\"m2\",\"beforeMessageId\":\"m9\",\"max\":20,\"version\":3}",
            json);

        ReadTranscript result = Assert.IsType<ReadTranscript>(ProtocolJson.Deserialize(json));
        Assert.Equal(read, result);
    }

    /// <summary>A <see cref="TranscriptTail"/> with one <see cref="ChatMessage"/> pins the exact wire shape (RS §6.5).</summary>
    [Fact]
    public void TranscriptTail_SerialisesToLiteralJson()
    {
        ChatMessage message = new("m5", new DateTimeOffset(2026, 9, 22, 16, 2, 11, TimeSpan.Zero), "human", "You", "go with option 2");
        TranscriptTail tail = new("r1", "room1", new List<ChatMessage> { message }, 12);

        string json = ProtocolJson.Serialize(tail);

        Assert.Equal(
            "{\"type\":\"transcriptTail\",\"requestId\":\"r1\",\"roomId\":\"room1\"," +
            "\"messages\":[{\"id\":\"m5\",\"timestamp\":\"2026-09-22T16:02:11+00:00\",\"senderId\":\"human\",\"senderName\":\"You\",\"text\":\"go with option 2\"}]," +
            "\"omitted\":12,\"version\":3}",
            json);

        TranscriptTail result = Assert.IsType<TranscriptTail>(ProtocolJson.Deserialize(json));

        // Not Assert.Equal(tail, result): Messages is a List<ChatMessage>, which has no value
        // equality of its own, so record equality would compare list references, not content.
        Assert.Equal(tail.RequestId, result.RequestId);
        Assert.Equal(tail.RoomId, result.RoomId);
        Assert.Equal(tail.Omitted, result.Omitted);
        Assert.Equal(tail.Messages, result.Messages);
    }

    /// <summary>Adding the two RS §6.5 Envelopes is additive, so it costs no <see cref="ProtocolVersion"/> bump.</summary>
    [Fact]
    public void ProtocolVersion_IsStill3()
    {
        Assert.Equal(3, ProtocolVersion.Current);
    }

    /// <summary>The optional Team on <see cref="MessagePosted"/> and <see cref="RoomInfo"/> round-trips when set.</summary>
    [Fact]
    public void Team_OnMessagePostedAndRoomInfo_RoundTrips()
    {
        ChatMessage chatMessage = new("m1", DateTimeOffset.UtcNow, "human", "You", "hi");
        MessagePosted posted = new("room-1", "echo", chatMessage, true, [], [], 0, 0, Team: "Marketing");
        Welcome welcome = new("agent-1", "echo", [new RoomInfo("room-1", "echo", [], Team: "Marketing")]);

        MessagePosted postedResult = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(ProtocolJson.Serialize(posted)));
        Welcome welcomeResult = Assert.IsType<Welcome>(ProtocolJson.Deserialize(ProtocolJson.Serialize(welcome)));

        Assert.Equal("Marketing", postedResult.Team);
        Assert.Equal("Marketing", Assert.Single(welcomeResult.Rooms).Team);
    }

    /// <summary>A root Room writes no <c>team</c> on the wire, and a message from a server built before the field reads as root.</summary>
    [Fact]
    public void Team_Absent_DeserialisesAsNullAndIsNotWritten()
    {
        ChatMessage chatMessage = new("m1", DateTimeOffset.UtcNow, "human", "You", "hi");
        string json = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", chatMessage, true, [], [], 0, 0));

        MessagePosted result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.DoesNotContain("\"team\"", json, StringComparison.Ordinal);
        Assert.Null(result.Team);
    }

    /// <summary>A Started ack carries the reply id and no outcome, and reads back.</summary>
    [Fact]
    public void QueueAck_Started_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new QueueAck("r1", ["m1", "m2"], QueueAckKind.Started, "reply-1"));

        Assert.Equal("{\"type\":\"queueAck\",\"roomId\":\"r1\",\"messageIds\":[\"m1\",\"m2\"],\"kind\":\"started\",\"replyMessageId\":\"reply-1\",\"version\":3}", json);
        QueueAck back = Assert.IsType<QueueAck>(ProtocolJson.Deserialize(json));
        Assert.Equal(["m1", "m2"], back.MessageIds);
        Assert.Equal(QueueAckKind.Started, back.Kind);
        Assert.Equal("reply-1", back.ReplyMessageId);
        Assert.Null(back.Outcome);
    }

    /// <summary>A Finished ack carries the outcome and no reply id, and reads back.</summary>
    [Fact]
    public void QueueAck_Finished_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new QueueAck("r1", ["m1"], QueueAckKind.Finished, null, QueueOutcome.Declined));

        Assert.Equal("{\"type\":\"queueAck\",\"roomId\":\"r1\",\"messageIds\":[\"m1\"],\"kind\":\"finished\",\"outcome\":\"declined\",\"version\":3}", json);
        QueueAck back = Assert.IsType<QueueAck>(ProtocolJson.Deserialize(json));
        Assert.Equal(QueueAckKind.Finished, back.Kind);
        Assert.Equal(QueueOutcome.Declined, back.Outcome);
        Assert.Null(back.ReplyMessageId);
    }

    /// <summary>A Held ack carries neither a reply id nor an outcome, and reads back.</summary>
    [Fact]
    public void QueueAck_Held_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new QueueAck("r1", ["m1"], QueueAckKind.Held));

        Assert.Equal("{\"type\":\"queueAck\",\"roomId\":\"r1\",\"messageIds\":[\"m1\"],\"kind\":\"held\",\"version\":3}", json);
        QueueAck back = Assert.IsType<QueueAck>(ProtocolJson.Deserialize(json));
        Assert.Equal(QueueAckKind.Held, back.Kind);
        Assert.Null(back.ReplyMessageId);
        Assert.Null(back.Outcome);
    }

    /// <summary>Every outcome name is camelCase on the wire.</summary>
    [Fact]
    public void QueueAck_EveryOutcome_IsCamelCase()
    {
        foreach (QueueOutcome outcome in Enum.GetValues<QueueOutcome>())
        {
            string json = ProtocolJson.Serialize(new QueueAck("r1", ["m1"], QueueAckKind.Finished, null, outcome));
            string expected = char.ToLowerInvariant(outcome.ToString()[0]) + outcome.ToString()[1..];

            Assert.Equal(expected, WireString(json, "outcome"));
            Assert.Equal(outcome, Assert.IsType<QueueAck>(ProtocolJson.Deserialize(json)).Outcome);
        }
    }

    /// <summary>Hello writes <c>durableQueue</c> only when set, and a Hello without it reads as null.</summary>
    [Fact]
    public void Hello_DurableQueue_WithAndWithout()
    {
        string with = ProtocolJson.Serialize(new Hello("x", null, true));
        string without = ProtocolJson.Serialize(new Hello("x", null));

        Assert.Equal("{\"type\":\"hello\",\"name\":\"x\",\"durableQueue\":true,\"version\":3}", with);
        Assert.Equal("{\"type\":\"hello\",\"name\":\"x\",\"version\":3}", without);
        Assert.True(Assert.IsType<Hello>(ProtocolJson.Deserialize(with)).DurableQueue);
        Assert.Null(Assert.IsType<Hello>(ProtocolJson.Deserialize(without)).DurableQueue);
    }

    /// <summary>MessagePosted writes <c>redelivery</c> only when set, and a line without it reads as null.</summary>
    [Fact]
    public void MessagePosted_Redelivery_WithAndWithout()
    {
        ChatMessage chatMessage = new("m1", DateTimeOffset.UnixEpoch, "human", "You", "hi");
        string with = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", chatMessage, true, [], [], 0, 0, null, Redelivery.CutOff));
        string waiting = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", chatMessage, true, [], [], 0, 0, null, Redelivery.Waiting));
        string without = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", chatMessage, true, [], [], 0, 0));

        Assert.Equal("cutOff", WireString(with, "redelivery"));
        Assert.Equal("waiting", WireString(waiting, "redelivery"));
        Assert.DoesNotContain("redelivery", without, StringComparison.Ordinal);
        Assert.Equal(Redelivery.CutOff, Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(with)).Redelivery);
        Assert.Null(Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(without)).Redelivery);
    }

    /// <summary>PostMessage writes <c>inReplyTo</c> only when set, and a line without it reads as null.</summary>
    [Fact]
    public void PostMessage_InReplyTo_WithAndWithout()
    {
        string with = ProtocolJson.Serialize(new PostMessage("r1", "m9", "hi", ["m1", "m2"]));
        string without = ProtocolJson.Serialize(new PostMessage("r1", "m9", "hi"));

        Assert.Equal("{\"type\":\"postMessage\",\"roomId\":\"r1\",\"messageId\":\"m9\",\"text\":\"hi\",\"inReplyTo\":[\"m1\",\"m2\"],\"version\":3}", with);
        Assert.DoesNotContain("inReplyTo", without, StringComparison.Ordinal);
        Assert.Equal(["m1", "m2"], Assert.IsType<PostMessage>(ProtocolJson.Deserialize(with)).InReplyTo);
        Assert.Null(Assert.IsType<PostMessage>(ProtocolJson.Deserialize(without)).InReplyTo);
    }

    /// <summary>GoldfishBegin pins its JSON, type first and version last, and reads back.</summary>
    [Fact]
    public void GoldfishBegin_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new GoldfishBegin("p1", "r1", "b"));

        Assert.Equal("{\"type\":\"goldfishBegin\",\"possessionId\":\"p1\",\"roomId\":\"r1\",\"brief\":\"b\",\"version\":3}", json);
        GoldfishBegin back = Assert.IsType<GoldfishBegin>(ProtocolJson.Deserialize(json));
        Assert.Equal("p1", back.PossessionId);
        Assert.Equal("r1", back.RoomId);
        Assert.Equal("b", back.Brief);
    }

    /// <summary>GoldfishEnd pins its JSON with a camelCase exit and reads back.</summary>
    [Fact]
    public void GoldfishEnd_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new GoldfishEnd("p1", GoldfishExit.CalledBack));

        Assert.Equal("{\"type\":\"goldfishEnd\",\"possessionId\":\"p1\",\"exit\":\"calledBack\",\"version\":3}", json);
        GoldfishEnd back = Assert.IsType<GoldfishEnd>(ProtocolJson.Deserialize(json));
        Assert.Equal("p1", back.PossessionId);
        Assert.Equal(GoldfishExit.CalledBack, back.Exit);
    }

    /// <summary>Every exit name is camelCase on the wire and reads back.</summary>
    [Fact]
    public void GoldfishEnd_EveryExit_IsCamelCase()
    {
        Assert.Equal(5, Enum.GetValues<GoldfishExit>().Length);
        foreach (GoldfishExit exit in Enum.GetValues<GoldfishExit>())
        {
            string json = ProtocolJson.Serialize(new GoldfishEnd("p1", exit));
            string expected = char.ToLowerInvariant(exit.ToString()[0]) + exit.ToString()[1..];

            Assert.Equal(expected, WireString(json, "exit"));
            Assert.Equal(exit, Assert.IsType<GoldfishEnd>(ProtocolJson.Deserialize(json)).Exit);
        }
    }

    /// <summary>A TurnEnded report carries Done, omits the other nulls, and reads back.</summary>
    [Fact]
    public void GoldfishReport_TurnEnded_PinsTheJson()
    {
        string json = ProtocolJson.Serialize(new GoldfishReport("p1", GoldfishReportKind.TurnEnded, Done: true));

        Assert.Equal("{\"type\":\"goldfishReport\",\"possessionId\":\"p1\",\"kind\":\"turnEnded\",\"done\":true,\"version\":3}", json);
        GoldfishReport back = Assert.IsType<GoldfishReport>(ProtocolJson.Deserialize(json));
        Assert.Equal(GoldfishReportKind.TurnEnded, back.Kind);
        Assert.True(back.Done);
        Assert.Null(back.RunningSpend);
        Assert.Null(back.Currency);
        Assert.Null(back.Detail);
    }

    /// <summary>A report with spend, currency and detail writes them in order and reads them back.</summary>
    [Fact]
    public void GoldfishReport_Failed_CarriesSpendAndDetail()
    {
        string json = ProtocolJson.Serialize(new GoldfishReport("p1", GoldfishReportKind.Failed, 1.25m, "USD", "boom"));

        Assert.Equal("{\"type\":\"goldfishReport\",\"possessionId\":\"p1\",\"kind\":\"failed\",\"runningSpend\":1.25,\"currency\":\"USD\",\"detail\":\"boom\",\"version\":3}", json);
        GoldfishReport back = Assert.IsType<GoldfishReport>(ProtocolJson.Deserialize(json));
        Assert.Equal(1.25m, back.RunningSpend);
        Assert.Equal("USD", back.Currency);
        Assert.Equal("boom", back.Detail);
        Assert.Null(back.Done);
    }

    /// <summary>Every report kind is camelCase on the wire and reads back.</summary>
    [Fact]
    public void GoldfishReport_EveryKind_IsCamelCase()
    {
        Assert.Equal(4, Enum.GetValues<GoldfishReportKind>().Length);
        foreach (GoldfishReportKind kind in Enum.GetValues<GoldfishReportKind>())
        {
            string json = ProtocolJson.Serialize(new GoldfishReport("p1", kind));
            string expected = char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

            Assert.Equal(expected, WireString(json, "kind"));
            Assert.Equal(kind, Assert.IsType<GoldfishReport>(ProtocolJson.Deserialize(json)).Kind);
        }
    }

    /// <summary>A goldfish line with an unknown extra property still reads.</summary>
    [Fact]
    public void GoldfishEnd_UnknownProperty_StillDeserialises()
    {
        const string json = "{\"type\":\"goldfishEnd\",\"possessionId\":\"p1\",\"exit\":\"failed\",\"extra\":1,\"version\":3}";

        GoldfishEnd back = Assert.IsType<GoldfishEnd>(ProtocolJson.Deserialize(json));

        Assert.Equal(GoldfishExit.Failed, back.Exit);
    }

    /// <summary>An unknown message type throws, which is why a server never sends a new type to an old runner.</summary>
    [Fact]
    public void Deserialize_UnknownType_Throws()
    {
        const string json = "{\"type\":\"goldfishNope\",\"possessionId\":\"p1\",\"version\":3}";

        Assert.ThrowsAny<JsonException>(() => ProtocolJson.Deserialize(json));
    }

    /// <summary>A ChatMessage with a goldfish id writes it and reads it back; one without omits it.</summary>
    [Fact]
    public void ChatMessage_Goldfish_WithAndWithout()
    {
        ChatMessage with = new("m1", DateTimeOffset.UnixEpoch, "human", "You", "hi") { Goldfish = "p1" };
        ChatMessage without = new("m1", DateTimeOffset.UnixEpoch, "human", "You", "hi");
        string withJson = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", with, true, [], [], 0, 0));
        string withoutJson = ProtocolJson.Serialize(new MessagePosted("room-1", "echo", without, true, [], [], 0, 0));

        Assert.Contains("\"goldfish\":\"p1\"", withJson, StringComparison.Ordinal); // contains-ok: one property inside a MessagePosted envelope; the round trip below pins the value
        Assert.DoesNotContain("goldfish", withoutJson, StringComparison.Ordinal);
        Assert.Equal("p1", Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(withJson)).Message.Goldfish);
        Assert.Null(Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(withoutJson)).Message.Goldfish);
    }

    private static string? WireString(string json, string property)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetString();
    }

    /// <summary>The additions do not bump the protocol version.</summary>
    [Fact]
    public void ProtocolVersion_StaysThree()
    {
        Assert.Equal(3, ProtocolVersion.Current);
    }
}