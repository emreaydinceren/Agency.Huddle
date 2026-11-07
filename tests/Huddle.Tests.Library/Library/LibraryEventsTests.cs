using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the <see cref="LibraryEvents"/> hub: every raise reaches every subscriber with its argument, and a subscriber that throws neither stops the next nor reaches the raiser (S-A2).</summary>
public sealed class LibraryEventsTests
{
    /// <summary>A raised HumanSaved reaches both subscribers with the saved path.</summary>
    [Fact]
    public void RaiseHumanSaved_ReachesEverySubscriberWithThePath()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryEvents events = new();
        List<LibraryPath> first = [];
        List<LibraryPath> second = [];
        events.HumanSaved += first.Add;
        events.HumanSaved += second.Add;

        events.RaiseHumanSaved(path);

        Assert.Equal(path.FullPath, Assert.Single(first).FullPath);
        Assert.Equal(path.FullPath, Assert.Single(second).FullPath);
    }

    /// <summary>A raised HumanCreated reaches both subscribers with the created path.</summary>
    [Fact]
    public void RaiseHumanCreated_ReachesEverySubscriberWithThePath()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryPath path = fixture.Resolve(vault, "plan.md");
        LibraryEvents events = new();
        List<LibraryPath> first = [];
        List<LibraryPath> second = [];
        events.HumanCreated += first.Add;
        events.HumanCreated += second.Add;

        events.RaiseHumanCreated(path);

        Assert.Equal(path.FullPath, Assert.Single(first).FullPath);
        Assert.Equal(path.FullPath, Assert.Single(second).FullPath);
    }

    /// <summary>A raised Moved reaches both subscribers with the same From and To.</summary>
    [Fact]
    public void RaiseMoved_ReachesEverySubscriberWithFromAndTo()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryPath from = fixture.Resolve(vault, "a.md");
        LibraryPath to = fixture.Resolve(vault, "b.md");
        LibraryEvents events = new();
        List<LibraryItemMoved> first = [];
        List<LibraryItemMoved> second = [];
        events.Moved += first.Add;
        events.Moved += second.Add;

        events.RaiseMoved(new LibraryItemMoved(from, to));

        LibraryItemMoved seenFirst = Assert.Single(first);
        LibraryItemMoved seenSecond = Assert.Single(second);
        Assert.Equal((from.FullPath, to.FullPath), (seenFirst.From.FullPath, seenFirst.To.FullPath));
        Assert.Equal((from.FullPath, to.FullPath), (seenSecond.From.FullPath, seenSecond.To.FullPath));
    }

    /// <summary>A HumanSaved subscriber that throws is skipped: the next one still runs and the raise does not throw.</summary>
    [Fact]
    public void RaiseHumanSaved_ThrowingSubscriber_NextStillRunsAndRaiseDoesNotThrow()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryEvents events = new();
        List<LibraryPath> seen = [];
        events.HumanSaved += _ => throw new InvalidOperationException("subscriber failed");
        events.HumanSaved += seen.Add;

        Exception? thrown = Record.Exception(() => events.RaiseHumanSaved(path));

        Assert.Null(thrown);
        Assert.Equal(path.FullPath, Assert.Single(seen).FullPath);
    }

    /// <summary>A Moved subscriber that throws is skipped: the next one still runs and the raise does not throw.</summary>
    [Fact]
    public void RaiseMoved_ThrowingSubscriber_NextStillRunsAndRaiseDoesNotThrow()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryPath from = fixture.Resolve(vault, "a.md");
        LibraryPath to = fixture.Resolve(vault, "b.md");
        LibraryEvents events = new();
        List<LibraryItemMoved> seen = [];
        events.Moved += _ => throw new InvalidOperationException("subscriber failed");
        events.Moved += seen.Add;

        Exception? thrown = Record.Exception(() => events.RaiseMoved(new LibraryItemMoved(from, to)));

        Assert.Null(thrown);
        Assert.Equal(to.FullPath, Assert.Single(seen).To.FullPath);
    }
}
