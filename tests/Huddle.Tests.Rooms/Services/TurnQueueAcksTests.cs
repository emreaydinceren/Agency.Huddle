using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Desktop;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Services;

/// <summary>What <see cref="TurnQueue"/> does with a client's acknowledgements, replies, refusals and a connection that ended (Turn Queue spec §5.5, §5.6).</summary>
public sealed class TurnQueueAcksTests
{
    /// <summary>A Started ack moves a waiting row to running, stamped with the connection that sent it.</summary>
    [Fact]
    public void Apply_Started_MovesWaitingRowToRunningOnThatConnection()
    {
        using Harness harness = new();
        harness.Seed("m1");

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));

        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1");
        Assert.Equal(("running", null, "c1"), (row.Status, row.Outcome, row.ConnectionId));
    }

    /// <summary>Each outcome a client can report lands as the same-named stored outcome on a done row.</summary>
    [Theory]
    [InlineData(QueueOutcome.Silent, "silent")]
    [InlineData(QueueOutcome.Failed, "failed")]
    [InlineData(QueueOutcome.Stopped, "stopped")]
    [InlineData(QueueOutcome.Declined, "declined")]
    [InlineData(QueueOutcome.Dropped, "dropped")]
    public void Apply_Finished_ClosesTheRowWithTheMappedOutcome(QueueOutcome outcome, string expected)
    {
        using Harness harness = new();
        harness.Seed("m1");
        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Finished, Outcome: outcome));

        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1");
        Assert.Equal(("done", expected), (row.Status, row.Outcome));
    }

    /// <summary>A Held ack changes nothing: the row stays waiting.</summary>
    [Fact]
    public void Apply_Held_LeavesTheRowWaiting()
    {
        using Harness harness = new();
        harness.Seed("m1");

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Held));

        Assert.Equal("waiting", TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1").Status);
    }

    /// <summary>A Started ack that arrives after the row was finished never reopens it.</summary>
    [Fact]
    public void Apply_StartedAfterFinished_DoesNotReopenTheRow()
    {
        using Harness harness = new();
        harness.Seed("m1");
        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Finished, Outcome: QueueOutcome.Stopped));

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));

        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1");
        Assert.Equal(("done", "stopped"), (row.Status, row.Outcome));
    }

    /// <summary>A Started ack for a Message with no row creates the row running.</summary>
    [Fact]
    public void Apply_StartedForMissingRow_CreatesItRunning()
    {
        using Harness harness = new();

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m9"], QueueAckKind.Started, "reply-9"));

        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m9");
        Assert.Equal(("running", "c1"), (row.Status, row.ConnectionId));
    }

    /// <summary>A reply closes rows whose Started ack never arrived as replied.</summary>
    [Fact]
    public void MarkReplied_RowStillWaiting_ClosesItAsReplied()
    {
        using Harness harness = new();
        harness.Seed("m1");

        harness.Queue.MarkReplied("a1", "r1", ["m1"]);

        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1");
        Assert.Equal(("done", "replied"), (row.Status, row.Outcome));
    }

    /// <summary>A refused reply closes its open rows as failed and leaves a row that is already done alone.</summary>
    [Fact]
    public void MarkRefused_ClosesOpenRowsAsFailedAndKeepsDoneRows()
    {
        using Harness harness = new();
        harness.Seed("m1");
        harness.Seed("m2");
        harness.Queue.MarkReplied("a1", "r1", ["m2"]);

        harness.Queue.MarkRefused("a1", "r1", ["m1", "m2"]);

        Assert.Equal(("done", "failed"), Outcome(harness, "m1"));
        Assert.Equal(("done", "replied"), Outcome(harness, "m2"));
    }

    /// <summary>Closing a connection stops only the running rows that connection started.</summary>
    [Fact]
    public void CloseRunning_ClosesOnlyThatConnectionsRunningRows()
    {
        using Harness harness = new();
        harness.Seed("m1");
        harness.Seed("m2");
        harness.Seed("m3");
        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));
        harness.Queue.Apply("c2", "a1", new QueueAck("r1", ["m2"], QueueAckKind.Started, "reply-2"));

        harness.Queue.CloseRunning("c1");

        Assert.Equal(("done", "stopped"), Outcome(harness, "m1"));
        Assert.Equal(("running", null), Outcome(harness, "m2"));
        Assert.Equal(("waiting", null), Outcome(harness, "m3"));
    }

    /// <summary>A Started ack naming a Room the Message's row is not in leaves that row waiting.</summary>
    [Fact]
    public void Apply_StartedNamingAnotherRoom_LeavesTheRowWaiting()
    {
        using Harness harness = new();
        harness.Seed("m1");

        harness.Queue.Apply("c1", "a1", new QueueAck("rOther", ["m1"], QueueAckKind.Started, "reply-1"));

        Assert.Equal(("waiting", null), Outcome(harness, "m1"));
        Assert.Null(TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", "m1").ConnectionId);
    }

    /// <summary>Once the host announced it is stopping, a closing connection leaves its running rows for the next start to resume.</summary>
    [Fact]
    public void CloseRunning_AfterApplicationStopping_LeavesRowsRunning()
    {
        using Harness harness = new();
        harness.Seed("m1");
        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));
        harness.Lifetime.StopApplication();

        harness.Queue.CloseRunning("c1");

        Assert.Equal(("running", null), Outcome(harness, "m1"));
    }

    /// <summary>With the Turn Queue switched off every ack, reply and close is ignored, but the subscription flag is still recorded.</summary>
    [Fact]
    public void Disabled_IgnoresAcksButStillRecordsTheSubscription()
    {
        using Harness harness = new(enabled: false);
        harness.Seed("m1");
        harness.Seed("m2");
        harness.Store.Started("a1", "r1", ["m2"], "reply-0", "c1", Harness.Start, Harness.Start);

        harness.Queue.Apply("c1", "a1", new QueueAck("r1", ["m1"], QueueAckKind.Started, "reply-1"));
        harness.Queue.MarkReplied("a1", "r1", ["m1"]);
        harness.Queue.MarkRefused("a1", "r1", ["m1"]);
        harness.Queue.CloseRunning("c1");
        harness.Queue.SetSubscribed("a1", durableQueue: true);

        Assert.Equal(("waiting", null), Outcome(harness, "m1"));
        Assert.Equal(("running", null), Outcome(harness, "m2"));
        Assert.Contains("a1", harness.Store.Subscribers());
    }

    /// <summary>A later Hello without the flag clears the subscription a former one set.</summary>
    [Fact]
    public void SetSubscribed_FalseAfterTrue_ClearsTheSubscription()
    {
        using Harness harness = new();

        harness.Queue.SetSubscribed("a1", durableQueue: true);
        Assert.Contains("a1", harness.Store.Subscribers());
        harness.Queue.SetSubscribed("a1", durableQueue: false);

        Assert.DoesNotContain("a1", harness.Store.Subscribers());
    }

    private static (string Status, string? Outcome) Outcome(Harness harness, string messageId)
    {
        TurnQueueRowSnapshot row = TurnQueueRows.Find(harness.Dir.Path, "a1", "r1", messageId);
        return (row.Status, row.Outcome);
    }

    /// <summary>A queue over a real store in a temp directory, with a lifetime the test can fire.</summary>
    private sealed class Harness : IDisposable
    {
        internal static readonly DateTimeOffset Start = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

        internal Harness(bool enabled = true)
        {
            this.Dir = new TempDataDir();
            Microsoft.Extensions.Options.IOptions<App.TeamOptions> options = this.Dir.Options(o => o.TurnQueue.Enabled = enabled);
            this.Store = new TurnQueueStore(options);
            this.Lifetime = new FakeLifetime();
            this.Queue = new TurnQueue(this.Store, options, NullLogger<TurnQueue>.Instance, this.Lifetime, new FakeTimeProvider(Start));
        }

        internal TempDataDir Dir { get; }

        internal TurnQueueStore Store { get; }

        internal FakeLifetime Lifetime { get; }

        internal TurnQueue Queue { get; }

        /// <summary>Writes one waiting row of Agent a1 in Room r1.</summary>
        /// <param name="messageId">The Message.</param>
        internal void Seed(string messageId) => _ = this.Store.Record("a1", messageId, "r1", Start, Start);

        public void Dispose()
        {
            this.Lifetime.Dispose();
            this.Dir.Dispose();
        }
    }
}
