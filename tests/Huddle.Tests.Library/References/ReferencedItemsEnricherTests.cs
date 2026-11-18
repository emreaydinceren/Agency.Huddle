using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Pins which Agent <see cref="ReferencedItemsEnricher.ForPersonas"/> treats as the Chief of Staff (issue 581): the
/// Persona carrying the built-in marker, by Name ignoring case, the check PR 580 gave <c>search</c> and
/// <c>read_records</c>.
/// </summary>
public sealed class ReferencedItemsEnricherTests
{
    private const string Limit = "You cannot read the Messages of a Room you are not a Member of.";

    /// <summary>
    /// The Persona carrying the Chief of Staff marker gets the hint naming <c>search</c>, in any casing of its Name; a
    /// Persona titled Chief of Staff without the marker, and any other Agent, keep the limit.
    /// </summary>
    /// <param name="personaName">The Turn's Persona name.</param>
    /// <param name="chiefOfStaff">Whether the hint must be the Chief of Staff's.</param>
    [Theory]
    [InlineData("Alfred", true)]
    [InlineData("alfred", true)]
    [InlineData("Jarvis", false)]
    [InlineData("Nova", false)]
    public async Task ForPersonas_OnlyTheMarkedPersonaGetsTheChiefOfStaffHint(string personaName, bool chiefOfStaff)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using PersonaStore personas = new(
            new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance);
        _ = personas.Add(new PersonaIdentity("Alfred", "Chief of Staff", "alf", [], Builtin: BuiltinAgent.ChiefOfStaffMarker), "You are Alfred.");
        _ = personas.Add(new PersonaIdentity("Jarvis", "Chief of Staff", "jar", []), "You are Jarvis.");
        _ = personas.Add(TestPersonas.Identity("Nova"), "You are Nova.");
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ReferencedItemsEnricher enricher = ReferencedItemsEnricher.ForPersonas(
            new ReferenceExpander(fixture.Resolver(), new FakePromptSource(), TestOptions.Create()), personas);
        WorkItem item = new("room-1", "Room 1", "Human", $"see [Launch](/rooms/{room.Id})", [], TriggerMessageId: "trig-1");

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, personaName, null), ct);

        Assert.NotNull(enriched.ReferencedItems);
        Assert.Equal(!chiefOfStaff, enriched.ReferencedItems.Contains(Limit, StringComparison.Ordinal));
        Assert.Equal(chiefOfStaff, enriched.ReferencedItems.Contains("with mcp__team__search:", StringComparison.Ordinal));
    }

    /// <summary>A Room referenced only in the second Message of a Folded Turn still gets its Referenced items header.</summary>
    [Fact]
    public async Task ForPersonas_ReferenceInALaterFoldedMessage_GetsTheHeader()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using PersonaStore personas = new(
            new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance);
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ReferencedItemsEnricher enricher = ReferencedItemsEnricher.ForPersonas(
            new ReferenceExpander(fixture.Resolver(), new FakePromptSource(), TestOptions.Create()), personas);
        string second = $"see [Launch](/rooms/{room.Id})";
        WorkItem item = new("room-1", "Room 1", "Human", "hello", [], TriggerMessageId: "t1", Foldable: true,
            Folded: [new FoldedMessage("t1", "Human", "hello", null), new FoldedMessage("t2", "Human", second, null)]);

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, "Nova", null), ct);

        Assert.NotNull(enriched.ReferencedItems);
    }

    private static TaskItem TaskFile(string id, string title) => Tasks.TestTasks.Make(
        id: id,
        title: title,
        status: TaskState.InProgress,
        assignee: "Nova",
        location: new TaskLocation("Platform", null, false));

    /// <summary>Builds the enricher, a Room and a Task in the fixture, and a Message that links the Room and names the Task.</summary>
    private static async Task<(ReferencedItemsEnricher Enricher, WorkItem Item)> RoomAndTaskAsync(ReferenceFixture fixture, CancellationToken ct)
    {
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ReferencedItemsEnricher enricher = new(
            new ReferenceExpander(fixture.Resolver(), new FakePromptSource(), TestOptions.Create()), static _ => false);
        WorkItem item = new("room-1", "Room 1", "Human", $"see [Launch](/rooms/{room.Id}) and PLAT-0001", [], TriggerMessageId: "trig-1");
        return (enricher, item);
    }

    /// <summary>A clean Turn whose Message links only a Room gets no Referenced items header: a Room is not the brief's to expose.</summary>
    [Fact]
    public async Task EnrichAsync_CleanAndOnlyARoomLink_HasNoHeader()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, TaskFile("PLAT-0001", "Fix login"));
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ReferencedItemsEnricher enricher = new(
            new ReferenceExpander(fixture.Resolver(), new FakePromptSource(), TestOptions.Create()), static _ => false);
        WorkItem item = new("room-1", "Room 1", "Human", $"see [Launch](/rooms/{room.Id})", [], TriggerMessageId: "trig-1");

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, "Nova", null, Clean: true), ct);

        Assert.Null(enriched.ReferencedItems);
    }

    /// <summary>A clean Turn keeps a Task reference and drops the Room link beside it: the header describes the Task only.</summary>
    [Fact]
    public async Task EnrichAsync_CleanWithTaskAndRoom_DescribesOnlyTheTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, TaskFile("PLAT-0001", "Fix login"));
        (ReferencedItemsEnricher enricher, WorkItem item) = await RoomAndTaskAsync(fixture, ct);

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, "Nova", null, Clean: true), ct);

        Assert.NotNull(enriched.ReferencedItems);
        Assert.Contains("Fix login", enriched.ReferencedItems, StringComparison.Ordinal); // contains-ok: the header's own text is the FakePromptSource's, the Task title is the pinned part
        Assert.DoesNotContain("Launch", enriched.ReferencedItems, StringComparison.Ordinal);
    }

    /// <summary>An ordinary Turn with the same Message describes the Room and the Task both, so the clean flag is what changed the header.</summary>
    [Fact]
    public async Task EnrichAsync_NotClean_DescribesTheRoomAndTheTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, TaskFile("PLAT-0001", "Fix login"));
        (ReferencedItemsEnricher enricher, WorkItem item) = await RoomAndTaskAsync(fixture, ct);

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, "Nova", null), ct);

        Assert.NotNull(enriched.ReferencedItems);
        Assert.Contains("Fix login", enriched.ReferencedItems, StringComparison.Ordinal); // contains-ok: the header's own text is the FakePromptSource's, the Task title is the pinned part
        Assert.Contains("Launch", enriched.ReferencedItems, StringComparison.Ordinal); // contains-ok: as above, the Room name is the pinned part
    }
}
