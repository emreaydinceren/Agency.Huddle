using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tabs;

/// <summary>
/// Pins that <see cref="TabSnapshot.MapTeamNames"/> is total over hand-edited storage (Team Rename batch 3 review): it runs before
/// <see cref="TabService.Restore"/> validates, so a snapshot with a missing root, group, Tab list, address or title must neither
/// throw nor change what <see cref="TabService.Restore"/> then decides.
/// </summary>
public sealed class TabSnapshotMalformedStorageTests
{
    /// <summary>Each malformed shape maps without throwing, and Restore treats the mapped snapshot as it treats the stored one.</summary>
    [Theory]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"root":{"kind":"group","activeId":"t1"}}""")]
    [InlineData("""{"version":1,"root":{"kind":"group","activeId":"t1","tabs":[{"id":"t1","title":"X","icon":"page","preview":false}]}}""")]
    [InlineData("""{"version":1,"root":{"kind":"group","activeId":"t1","tabs":[{"id":"t1","url":"/teams/","title":"Team","icon":"page","preview":false}]}}""")]
    [InlineData("""{"version":1,"root":{"kind":"group","activeId":"t1","tabs":[{"id":"t1","url":"/teams/Marketing","title":"","icon":"page","preview":false}]}}""")]
    [InlineData("""{"version":2,"root":{"kind":"split","tabs":[],"children":[{"kind":"group"},{"kind":"group","tabs":[]}]}}""")]
    public void MapTeamNames_MalformedStorage_DoesNotThrowAndRestoreBehavesAsBefore(string json)
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        TabSnapshot? stored = TabSnapshot.TryParse(json);
        Assert.NotNull(stored);

        TabSnapshot mapped = stored.MapTeamNames(journal);

        TabService plain = new();
        TabService afterMap = new();
        bool plainResult = plain.Restore(stored);
        bool mappedResult = afterMap.Restore(mapped);
        Assert.Equal(plainResult, mappedResult);
        Assert.Equal(plain.Tabs.Select(static t => t.Url), afterMap.Tabs.Select(static t => t.Url));
    }

    /// <summary>A Project route with an empty Project segment still maps its Team to today's name and does not throw.</summary>
    [Fact]
    public void MapTeamNames_EmptyProjectSegment_MapsTheTeamWithoutThrowing()
    {
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        TabSnapshot? stored = TabSnapshot.TryParse("""{"version":1,"root":{"kind":"group","activeId":"t1","tabs":[{"id":"t1","url":"/teams/Marketing/projects/","title":"Team","icon":"page","preview":false}]}}""");
        Assert.NotNull(stored);

        TabSnapshot mapped = stored.MapTeamNames(journal);

        TabService restored = new();
        Assert.True(restored.Restore(mapped));
        Assert.StartsWith("/teams/Growth", Assert.Single(restored.Tabs).Url, StringComparison.Ordinal);
    }
}
