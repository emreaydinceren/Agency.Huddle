using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins cancellation of the index build: a walk cancelled between two files stops, and nothing half-built is remembered.</summary>
public sealed class WikiLinkIndexCancellationTests
{
    private const int FileCount = 20;

    /// <summary>A build cancelled from inside the walk, after its first file, throws; the next uncancelled call builds the complete index.</summary>
    [Fact]
    public void Build_CancelledMidWalk_Throws_AndTheNextCallBuildsEverything()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = fixture.CreatePinnedRoot("root");
        for (int i = 0; i < FileCount; i++)
        {
            File.WriteAllText(Path.Combine(root, $"n{i:00}.md"), "---\naliases: [shared]\n---\n");
        }

        fixture.Reload();
        string rootId = fixture.Resolve(root, "n00.md").Root.Id;
        using CancellationTokenSource cancel = new();
        int filesSeen = 0;
        WikiLinkIndex index = new(fixture.RootStore, fixture.Resolver, Options.Create(new TeamOptions()), afterWalkForTests: null, afterFileForTests: () =>
        {
            if (++filesSeen == 1)
            {
                cancel.Cancel();
            }
        });

        _ = Assert.Throws<OperationCanceledException>(() => index.NotesByAlias(rootId, "shared", cancel.Token));

        Assert.Equal(FileCount, index.NotesByAlias(rootId, "shared", TestContext.Current.CancellationToken).Count);
        Assert.Equal(FileCount, index.FilesUnder(rootId, string.Empty).Count);
    }
}
