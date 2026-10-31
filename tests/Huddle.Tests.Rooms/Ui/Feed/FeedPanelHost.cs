using Agency.Huddle.App;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A real <see cref="FeedStore"/>, <see cref="FeedQueue"/> and <see cref="FeedEvents"/> on a temporary data folder for the Notifications
/// button and panel tests. There is no writer: a test posts straight into the store and applies what the UI queued with <see cref="ApplyQueued"/>.
/// </summary>
internal sealed class FeedPanelHost : IDisposable
{
    private readonly TempDataDir dir = new();
    private long nextTarget;

    /// <summary>Builds the host.</summary>
    /// <param name="configure">Changes the Feed options before anything is built.</param>
    public FeedPanelHost(Action<FeedOptions>? configure = null)
    {
        this.Options = this.dir.Options(options => configure?.Invoke(options.Feed));
        this.Store = new FeedStore(this.Options);
        this.Queue = new FeedQueue(this.Options, NullLogger<FeedQueue>.Instance, new ManualTimeProvider(FeedTestData.Now, TimeZoneInfo.Utc));
        this.Events = new FeedEvents();
    }

    /// <summary>The options the host was built with.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>The store.</summary>
    public FeedStore Store { get; }

    /// <summary>The queue the UI posts its commands to.</summary>
    public FeedQueue Queue { get; }

    /// <summary>The events hub.</summary>
    public FeedEvents Events { get; }

    /// <summary>Registers the host's services.</summary>
    /// <param name="services">The collection to register into.</param>
    public void AddTo(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(this.Options);
        services.AddSingleton(this.Store);
        services.AddSingleton(this.Queue);
        services.AddSingleton(this.Events);
        services.TryAddSingleton(TimeProvider.System);
    }

    /// <summary>Posts a Notification that a Task needs the Human: unresolved until <see cref="Resolve"/>.</summary>
    /// <param name="minutesAgo">How long before the fixed now it happened.</param>
    /// <returns>The stored item.</returns>
    public FeedItem PostNeedsYou(int minutesAgo = 0)
    {
        string id = this.NextId();
        FeedMessage message = new(FeedSourceType.TaskNeedsYou, id, new FeedActor(FeedActorKind.Agent, "Agent" + this.nextTarget), true, new TaskNeedsYouPayload(id, "Draft " + id, "Growth", 1, 2));
        return this.Store.Apply([new PostCommand(message, FeedTestData.Now.AddMinutes(-minutesAgo))]).Added.Single();
    }

    /// <summary>Posts an Activity item (not a Notification): a Task was created.</summary>
    /// <param name="minutesAgo">How long before the fixed now it happened.</param>
    /// <returns>The stored item.</returns>
    public FeedItem PostTaskCreated(int minutesAgo = 0)
    {
        string id = this.NextId();
        FeedMessage message = new(FeedSourceType.TaskCreated, id, new FeedActor(FeedActorKind.Agent, "Kai"), false, new TaskCreatedPayload(id, "Draft " + id, "Growth", null));
        return this.Store.Apply([new PostCommand(message, FeedTestData.Now.AddMinutes(-minutesAgo))]).Added.Single();
    }

    /// <summary>Resolves the Notification an item stands for, as the Task leaving the Needs you state does.</summary>
    /// <param name="item">A Task-needs-you item from <see cref="PostNeedsYou"/>.</param>
    public void Resolve(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _ = this.Store.Apply([new ResolveCommand(item.TargetKey, FeedSourceType.TaskNeedsYou, FeedTestData.Now)]);
    }

    /// <summary>Dismisses a Notification straight in the store.</summary>
    /// <param name="item">The Notification.</param>
    public void Dismiss(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _ = this.Store.Apply([new DismissCommand(item.Id, FeedTestData.Now)]);
    }

    /// <summary>Reads everything the UI has queued so far and applies it to the store, as the writer would.</summary>
    /// <returns>The commands, in queue order.</returns>
    public IReadOnlyList<FeedCommand> ApplyQueued()
    {
        List<FeedCommand> commands = [];
        while (this.Queue.Reader.TryRead(out FeedCommand? command))
        {
            commands.Add(command);
        }

        _ = this.Store.Apply(commands);
        return commands;
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private string NextId() => "GRO-" + ++this.nextTarget;
}
