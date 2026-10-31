using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Feed;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>What <see cref="DocHistoryStore"/> tells the Feed when an entry is appended (Feed spec §6.5.4).</summary>
public sealed class DocHistoryStoreFeedTests
{
    /// <summary>An Agent entry posts document-changed with the file path as target, the entry's kind and its summary.</summary>
    [Fact]
    public void Append_AgentEntry_PostsDocumentChanged()
    {
        using TempDataDir dataDir = new();
        RecordingFeedPublisher feed = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance, feed);
        App.Library.LibraryPath file = Document("Research/findings.md");

        store.Append(file, At(9, 12, "Miriam", DocChangeKind.Reviewed, "Checked the citations"));

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.DocumentChanged, post.SourceType);
        Assert.False(post.Notify);
        Assert.Equal(file.FullPath, post.TargetKey);
        Assert.Equal(new FeedActor(FeedActorKind.Agent, "Miriam"), post.Actor);
        DocumentChangedPayload payload = Assert.IsType<DocumentChangedPayload>(post.Payload);
        Assert.Equal(("reviewed", "Checked the citations", "Research/findings.md"), (payload.Kind, payload.Summary, payload.RelativePath));
    }

    /// <summary>The Human's own entries are not posted: one editing session would be dozens of items.</summary>
    [Fact]
    public void Append_HumanEntry_PostsNothing()
    {
        using TempDataDir dataDir = new();
        RecordingFeedPublisher feed = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance, feed);

        store.Append(Document("findings.md"), At(11, 2, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human));

        Assert.Empty(feed.Posts);
    }
}
