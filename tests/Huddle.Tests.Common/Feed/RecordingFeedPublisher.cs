using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>An <see cref="IFeedPublisher"/> that remembers every call, in order, for the producer tests.</summary>
internal sealed class RecordingFeedPublisher : IFeedPublisher
{
    private readonly List<FeedMessage> posts = [];
    private readonly List<FeedResolution> resolutions = [];

    /// <summary>Whether every call throws, to prove a producer survives a failing Feed.</summary>
    public bool Throws { get; init; }

    /// <summary>The items posted so far.</summary>
    public IReadOnlyList<FeedMessage> Posts => this.posts;

    /// <summary>The resolutions asked for so far.</summary>
    public IReadOnlyList<FeedResolution> Resolutions => this.resolutions;

    /// <inheritdoc />
    public void Post(FeedMessage message)
    {
        if (this.Throws)
        {
            throw new InvalidOperationException("The Feed is down.");
        }

        this.posts.Add(message);
    }

    /// <inheritdoc />
    public void Resolve(string targetKey, FeedSourceType type)
    {
        if (this.Throws)
        {
            throw new InvalidOperationException("The Feed is down.");
        }

        this.resolutions.Add(new FeedResolution(targetKey, type));
    }
}
