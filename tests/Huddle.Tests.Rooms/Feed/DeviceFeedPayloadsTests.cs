using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>The four remote-access Feed items (Remote access spec 6.14, OP-6): wire names, payloads, and the new Devices Category.</summary>
public sealed class DeviceFeedPayloadsTests
{
    private static readonly FeedSourceType[] DeviceTypes =
    [
        FeedSourceType.PairingRequested,
        FeedSourceType.AdminRequested,
        FeedSourceType.DevicePaired,
        FeedSourceType.DeviceRevoked,
    ];

    /// <summary>The four wire names are the ones the column holds forever, and each parses back.</summary>
    [Fact]
    public void WireName_DeviceTypes_AreFixedAndRoundTrip()
    {
        (FeedSourceType Type, string Wire)[] expected =
        [
            (FeedSourceType.PairingRequested, "pairing-requested"),
            (FeedSourceType.AdminRequested, "admin-requested"),
            (FeedSourceType.DevicePaired, "device-paired"),
            (FeedSourceType.DeviceRevoked, "device-revoked"),
        ];

        Assert.All(expected, row =>
        {
            Assert.Equal(row.Wire, FeedSourceTypes.ToWire(row.Type));
            Assert.True(FeedSourceTypes.TryParse(row.Wire, out FeedSourceType parsed));
            Assert.Equal(row.Type, parsed);
        });
    }

    /// <summary>Each type names its own payload record, and the sample matches only its own type.</summary>
    [Fact]
    public void TypeOf_DeviceTypes_NameTheirPayloads()
    {
        Assert.Equal(typeof(PairingRequestedPayload), FeedPayloads.TypeOf(FeedSourceType.PairingRequested));
        Assert.Equal(typeof(AdminRequestedPayload), FeedPayloads.TypeOf(FeedSourceType.AdminRequested));
        Assert.Equal(typeof(DevicePairedPayload), FeedPayloads.TypeOf(FeedSourceType.DevicePaired));
        Assert.Equal(typeof(DeviceRevokedPayload), FeedPayloads.TypeOf(FeedSourceType.DeviceRevoked));
        Assert.False(FeedPayloads.Matches(FeedSourceType.DevicePaired, FeedTestSupport.SamplePayload(FeedSourceType.DeviceRevoked)));
    }

    /// <summary>Each payload survives the envelope with the Huddle actor kind and reads back equal.</summary>
    [Fact]
    public void Envelope_DeviceTypes_RoundTripWithTheHuddleActor()
    {
        foreach (FeedSourceType type in DeviceTypes)
        {
            FeedPayload original = FeedTestSupport.SamplePayload(type);

            string json = FeedPayloads.Serialize(FeedActorKind.Huddle, original);
            FeedEnvelope read = FeedPayloads.Read(json, type);

            Assert.Equal(FeedActorKind.Huddle, read.ActorKind);
            Assert.Equal(original, read.Payload);
        }
    }

    /// <summary>A payload row that lost a required field reads as unreadable, not as nulls.</summary>
    [Fact]
    public void Read_DevicePairedMissingName_GivesNullPayload()
    {
        const string json = """{"v":1,"actorKind":"huddle","data":{"deviceId":"dev-1","pairedVia":"approval"}}""";

        Assert.Null(FeedPayloads.Read(json, FeedSourceType.DevicePaired).Payload);
    }

    /// <summary>All four types belong to the Devices Category and to no other.</summary>
    [Fact]
    public void Of_DeviceTypes_AreDevices()
    {
        Assert.All(DeviceTypes, type => Assert.Equal(FeedCategory.Devices, FeedCategories.Of(type)));
        Assert.Equal(DeviceTypes, FeedCategories.SourceTypes(FeedCategory.Devices));
        Assert.DoesNotContain(FeedSourceType.HelperPaused, FeedCategories.SourceTypes(FeedCategory.Devices));
    }

    /// <summary>The Devices segment counts the four device items, and a Helper item is not among them; an unresolved pairing request is also a Needs you item.</summary>
    [Fact]
    public void Counts_DeviceItems_FeedTheDevicesSegment()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        foreach (FeedSourceType type in DeviceTypes)
        {
            _ = FeedTestSupport.Post(store, new FeedMessage(type, "device:" + type, FeedActor.Huddle, type is FeedSourceType.PairingRequested or FeedSourceType.AdminRequested, FeedTestSupport.SamplePayload(type)));
        }

        _ = FeedTestSupport.Post(store, FeedTestSupport.Of(FeedSourceType.DocumentChanged, "C:/lib/a.md"));

        FeedCounts counts = store.Counts(null, null);

        Assert.Equal(4, counts.Devices);
        Assert.Equal(2, counts.NeedsYou);
        Assert.Equal(1, counts.Documents);
        Assert.Equal(4, FeedCategories.CountOf(counts, FeedCategory.Devices));
    }

    /// <summary>Devices is the Category the control draws just before Second Brain, labelled "Devices", and its query value round-trips.</summary>
    [Fact]
    public void Devices_Category_IsBeforeSecondBrainLabelledAndParses()
    {
        Assert.Equal(FeedCategory.Devices, FeedCategories.Order[^2]);
        Assert.Equal(FeedCategory.SecondBrain, FeedCategories.Order[^1]);
        Assert.Equal("Devices", FeedCategories.Label(FeedCategory.Devices));
        Assert.Equal("devices", FeedCategories.ToWire(FeedCategory.Devices));
        Assert.Equal(FeedCategory.Devices, FeedCategories.Parse("devices"));
    }
}
