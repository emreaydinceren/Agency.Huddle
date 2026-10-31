using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Feed;

/// <summary>A real store, queue, events hub and writer on a temporary data folder, with a manual clock and a recording logger.</summary>
internal sealed class FeedHarness : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly ConcurrentQueue<FeedItem> added = new();
    private int changedCount;

    /// <summary>Builds the harness.</summary>
    /// <param name="configure">Changes the Feed options before anything is built.</param>
    public FeedHarness(Action<FeedOptions>? configure = null)
    {
        this.Options = this.dir.Options(options => configure?.Invoke(options.Feed));
        this.Clock = new ManualTimeProvider(FeedTestSupport.T0);
        this.Store = new FeedStore(this.Options);
        this.Logger = new RecordingLogger();
        this.Queue = new FeedQueue(this.Options, new FeedTestSupport.TypedLogger<FeedQueue>(this.Logger), this.Clock);
        this.Events = new FeedEvents();
        this.Events.ItemsAdded += items =>
        {
            foreach (FeedItem item in items)
            {
                this.added.Enqueue(item);
            }
        };
        this.Events.ItemsChanged += () => Interlocked.Increment(ref this.changedCount);
        this.Directory = new SqliteTeamDirectory(this.Options);
        this.Writer = new FeedWriter(
            this.Queue, this.Store, this.Events, this.Directory, this.Options,
            new FeedTestSupport.TypedLogger<FeedWriter>(this.Logger), this.Clock);
    }

    /// <summary>The options the harness was built with.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The data folder.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The settable clock.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>The store.</summary>
    public FeedStore Store { get; }

    /// <summary>The queue.</summary>
    public FeedQueue Queue { get; }

    /// <summary>The events hub.</summary>
    public FeedEvents Events { get; }

    /// <summary>The Team directory the writer reads Rooms from.</summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary>The writer.</summary>
    public FeedWriter Writer { get; }

    /// <summary>What the queue and the writer logged.</summary>
    public RecordingLogger Logger { get; }

    /// <summary>Every item an <see cref="FeedEvents.ItemsAdded"/> delivered.</summary>
    public IReadOnlyCollection<FeedItem> Added => this.added;

    /// <summary>How many times <see cref="FeedEvents.ItemsChanged"/> was raised.</summary>
    public int ChangedCount => Volatile.Read(ref this.changedCount);

    /// <summary>Posts a sentinel, then waits until the writer has applied it, which means every command queued before it has been applied too.</summary>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>A task that completes when the sentinel's batch is committed.</returns>
    public async Task DrainAsync(CancellationToken ct)
    {
        string key = "sentinel-" + Guid.NewGuid().ToString("N");
        this.Queue.Post(FeedTestSupport.TaskCreated(key));
        await WaitUntilAsync(() => this.added.Any(item => string.Equals(item.TargetKey, key, StringComparison.Ordinal)), ct);
    }

    /// <summary>Polls until a condition holds, failing after ten seconds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    public static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the Feed writer.");
            await Task.Delay(5, ct);
        }
    }

    /// <summary>Initialises the Team directory and creates a Room.</summary>
    /// <param name="name">The Room name.</param>
    /// <param name="team">The Room's Team, or <see langword="null"/> for a root Room.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The Room.</returns>
    public async Task<Room> CreateRoomAsync(string name, string? team, CancellationToken ct)
    {
        await this.Directory.InitializeAsync("You", ct);
        return await this.Directory.CreateRoomAsync(name, [KnownIds.Human], team, ct: ct);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Writer.Dispose();
        this.dir.Dispose();
    }
}
