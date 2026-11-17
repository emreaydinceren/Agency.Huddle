using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
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
}
