using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// A subscriber that throws must neither stop the subscribers after it nor make the raising operation
/// look failed. The mechanism is <c>EventRaise</c>, tested on its own; these pin three of its busiest users.
/// </summary>
public sealed class EventIsolationTests
{
    /// <summary>A throwing <c>ViewsChanged</c> subscriber does not stop the next one, the save still reports success and the View is stored.</summary>
    [Fact]
    public void ViewStoreSave_ThrowingSubscriber_StillRunsTheNextAndTheSaveLands()
    {
        using TempDataDir dir = new();
        RecordingLogger<ViewStore> logger = new();
        using ViewStore store = new(dir.Options(), logger);
        int laterRan = 0;
        store.ViewsChanged += static () => throw new InvalidOperationException("subscriber failed");
        store.ViewsChanged += () => laterRan++;

        ViewSaveResult result = store.Save(new TaskView { Id = "custom-view", Name = "Sprint Board", Kind = ViewKind.List });

        Assert.True(result.Saved);
        Assert.Equal(1, laterRan);
        Assert.NotNull(store.Get("custom-view"));
        Assert.Single(logger.AtExactly(LogLevel.Error));
    }

    /// <summary>A throwing <c>AvatarsChanged</c> subscriber does not stop the next one, the save returns normally and the avatar is stored.</summary>
    [Fact]
    public void AvatarStoreSave_ThrowingSubscriber_StillRunsTheNextAndTheSaveLands()
    {
        using TempDataDir dir = new();
        using AvatarStore store = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        int laterRan = 0;
        store.AvatarsChanged += static () => throw new InvalidOperationException("subscriber failed");
        store.AvatarsChanged += () => laterRan++;
        Avatar saved = new(Label: "AB", Image: null, Background: "#336699");

        store.Save("coo", saved);

        Assert.Equal(1, laterRan);
        Assert.Equal(saved, store.Get("coo"));
    }

    /// <summary>A throwing <c>TasksReloaded</c> subscriber does not stop the next one and the raise returns normally.</summary>
    [Fact]
    public void TaskEvents_RaiseTasksReloaded_ThrowingSubscriber_StillRunsTheNext()
    {
        TaskEvents events = new();
        int laterRan = 0;
        events.TasksReloaded += static () => throw new InvalidOperationException("subscriber failed");
        events.TasksReloaded += () => laterRan++;

        events.RaiseTasksReloaded();

        Assert.Equal(1, laterRan);
    }

    /// <summary>A throwing <c>Changed</c> subscriber does not stop the next one and <c>Begin</c> still marks the Turn busy.</summary>
    [Fact]
    public void TurnActivityBegin_ThrowingSubscriber_StillRunsTheNextAndTheStateChanged()
    {
        TurnActivity activity = new();
        int laterRan = 0;
        activity.Changed += static () => throw new InvalidOperationException("subscriber failed");
        activity.Changed += () => laterRan++;

        activity.Begin("nova", "room-1");

        Assert.Equal(1, laterRan);
        Assert.True(activity.IsBusyIn("nova", "room-1"));
    }
}
