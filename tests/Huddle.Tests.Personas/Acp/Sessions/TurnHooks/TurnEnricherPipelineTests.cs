using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>
/// Pins <see cref="TurnEnricherPipeline"/>: the production order, the refusal of an unlisted name, that only a Message is
/// enriched, that a failing enricher is logged by name and skipped, and that cancellation is not swallowed.
/// </summary>
public sealed class TurnEnricherPipelineTests
{
    private static WorkItem Message() => new("room-1", "Room 1", "Human", "hello", [], TriggerMessageId: "trig-1");

    /// <summary>Enrichers handed over in reverse run in production order: Library documents, then Referenced items.</summary>
    [Fact]
    public void ForPersona_EnrichersInReverse_RunInProductionOrder()
    {
        ITurnEnricher[] reversed = [new MarkingEnricher("ReferencedItems"), new MarkingEnricher("LibraryDocuments")];

        TurnEnricherPipeline pipeline = TurnEnricherPipeline.ForPersona(reversed, NullLogger.Instance);

        Assert.Equal(["LibraryDocuments", "ReferencedItems"], pipeline.EnricherNames);
    }

    /// <summary>An enricher whose name is not in the production order is refused, and the message names it.</summary>
    [Fact]
    public void ForPersona_UnlistedEnricher_ThrowsNamingIt()
    {
        ITurnEnricher[] enrichers = [new MarkingEnricher("LibraryDocuments"), new MarkingEnricher("Unlisted")];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => TurnEnricherPipeline.ForPersona(enrichers, NullLogger.Instance));

        Assert.Equal(
            "Turn enricher 'Unlisted' is not in the production order; add its name to TurnEnricherPipeline.ProductionOrder.",
            error.Message);
    }

    /// <summary>A Message is passed through every enricher in order, each one seeing the previous one's result.</summary>
    [Fact]
    public async Task EnrichAsync_Message_RunsEnrichersInOrderEachOnThePreviousResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnEnricherPipeline pipeline = TurnEnricherPipeline.ForPersona(
            [new MarkingEnricher("ReferencedItems"), new MarkingEnricher("LibraryDocuments")],
            NullLogger.Instance);

        WorkItem result = await pipeline.EnrichAsync(Message(), "nova", null, ct);

        Assert.Equal("LibraryDocuments,ReferencedItems", result.ReferencedItems);
    }

    /// <summary>A Greeting and a Command come back untouched, and no enricher is called.</summary>
    /// <param name="greeting"><see langword="true"/> for a Greeting, <see langword="false"/> for a Command.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnrichAsync_NotAMessage_IsUntouched(bool greeting)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        WorkItemKind kind = greeting ? WorkItemKind.Greeting : WorkItemKind.Command;
        MarkingEnricher enricher = new("LibraryDocuments");
        TurnEnricherPipeline pipeline = TurnEnricherPipeline.ForPersona([enricher], NullLogger.Instance);
        WorkItem item = Message() with { Kind = kind };

        WorkItem result = await pipeline.EnrichAsync(item, "nova", null, ct);

        Assert.Same(item, result);
        Assert.Equal(0, enricher.Calls);
    }

    /// <summary>A throwing enricher is logged by name with the Persona and Room, and the later one still runs on the item as it was.</summary>
    [Fact]
    public async Task EnrichAsync_EnricherThrows_LogsItAndTheLaterOneStillRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger logger = new();
        TurnEnricherPipeline pipeline = TurnEnricherPipeline.ForPersona(
            [new ThrowingEnricher("LibraryDocuments"), new MarkingEnricher("ReferencedItems")],
            logger);

        WorkItem result = await pipeline.EnrichAsync(Message(), "nova", null, ct);

        Assert.Equal("ReferencedItems", result.ReferencedItems);
        Assert.Null(result.LibraryDocuments);
        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Turn enricher LibraryDocuments failed for Persona 'nova' in room room-1; the item is unchanged.", entry.Message);
    }

    /// <summary>A cancelled enricher is not swallowed: the pipeline throws, and nothing is logged.</summary>
    [Fact]
    public async Task EnrichAsync_Cancelled_Propagates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger logger = new();
        MarkingEnricher later = new("ReferencedItems");
        TurnEnricherPipeline pipeline = TurnEnricherPipeline.ForPersona(
            [new CancellingEnricher("LibraryDocuments"), later],
            logger);

        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.EnrichAsync(Message(), "nova", null, ct));

        Assert.Empty(logger.Entries);
        Assert.Equal(0, later.Calls);
    }

    /// <summary>Appends its name to <see cref="WorkItem.ReferencedItems"/> so the order and the hand-over are visible.</summary>
    private sealed class MarkingEnricher(string name) : ITurnEnricher
    {
        public int Calls { get; private set; }

        public string Name => name;

        public Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken)
        {
            this.Calls++;
            string marks = turn.Item.ReferencedItems is { } earlier ? earlier + "," + name : name;
            return Task.FromResult(turn.Item with { ReferencedItems = marks });
        }
    }

    /// <summary>Throws a plain failure, as a scan or a file read would.</summary>
    private sealed class ThrowingEnricher(string name) : ITurnEnricher
    {
        public string Name => name;

        public Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken) =>
            throw new IOException("the scan failed");
    }

    /// <summary>Throws the cancellation the run token would.</summary>
    private sealed class CancellingEnricher(string name) : ITurnEnricher
    {
        public string Name => name;

        public Task<WorkItem> EnrichAsync(TurnEnriching turn, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }
}
