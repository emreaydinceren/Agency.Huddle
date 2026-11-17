using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins that the Library enricher reads every Message of a Folded Turn (Turn Queue spec, section 6.4), each document once.</summary>
public sealed class FoldedTurnEnricherTests
{
    /// <summary>A Library path named only in the second and third Messages of a Folded Turn is attached once.</summary>
    [Fact]
    public async Task LibraryDocuments_NamedInLaterFoldedMessages_AreAttachedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TeamOptions captured = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(options => captured = options);
        string root = fixture.CreatePinnedRoot("Docs");
        string path = Path.Combine(root, "plan.md");
        File.WriteAllText(path, "plan");
        string uri = new Uri(path).AbsoluteUri;
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(captured));
        LibraryDocumentsEnricher enricher = new(collector, readsFiles: true, NullLogger.Instance);
        WorkItem item = new(
            "room-1", "Room 1", "Human", "first", [], TriggerMessageId: "t1", Foldable: true,
            Folded:
            [
                new FoldedMessage("t1", "Human", "first", null),
                new FoldedMessage("t2", "Human", $"see {uri}", null),
                new FoldedMessage("t3", "Human", $"and again {uri}", null),
            ]);

        WorkItem enriched = await enricher.EnrichAsync(new TurnEnriching(item, "nova", null), ct);

        LibraryDocumentsReport report = Assert.IsType<LibraryDocumentsReport>(enriched.LibraryDocuments);
        Assert.Equal(path, Assert.Single(report.Items).FullPath, StringComparer.OrdinalIgnoreCase);
    }
}
