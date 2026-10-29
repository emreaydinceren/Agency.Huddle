using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>The seeded Feed loads through Huddle's own <see cref="FeedStore"/>, and every unresolved Notification is still true of the rest of the seed.</summary>
public sealed class SeedFeedTests
{
    /// <summary>Every planned item is stored, and the Notifications that wait are the ones the plan leaves unresolved.</summary>
    [Fact]
    public async Task Feed_StoresEveryItem_AndTheWaitingNotificationsMatchThePlan()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        FeedStore store = new(seed.Options());

        IReadOnlyList<FeedItem> all = store.Page(new FeedQuery(null, 1000, null, null));
        IReadOnlyList<FeedItem> waiting = [.. all.Where(static item => item.Notify && item.ResolvedAt is null)];

        Assert.Equal(plan.Feed.Count, all.Count);
        Assert.Equal(plan.Feed.Count(static item => item.Notify && item.ResolvedMinutesAgo is null), waiting.Count);
        Assert.NotEmpty(waiting);
        Assert.Contains(all, static item => item.Notify && item.ResolvedAt is not null);
        Assert.Contains(all, static item => item.Actor.Kind == FeedActorKind.Helper);
        Assert.Equal(5, all.Count(static item => item.SourceType == FeedSourceType.TaskCreated && item.Actor.Name == "Owen"));
    }

    /// <summary>The Helper's stored state says it is at its cap and failing, so its Feed items still hold when Huddle starts.</summary>
    [Fact]
    public async Task HelperState_MatchesItsWaitingFeedItems()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        HelperId id = new("NeedsHuman");
        var options = seed.Options();

        HelperStatus status = new HelperStatusStore(options).Get(id);
        HelperBudget budget = new(new HelperUsageStore(options));

        Assert.Equal(3, status.ConsecutiveFailures);
        Assert.True(budget.IsPaused(id, 50));
    }

    /// <summary>The Tasks the waiting Task Notifications name are in the state those Notifications need, so the startup reconciler leaves them alone.</summary>
    [Fact]
    public async Task WaitingTaskNotifications_HoldAgainstTheSeededTasks()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        var options = seed.Options();
        using PersonaStore personas = new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
        using TaskStore tasks = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        FeedStore store = new(options);
        string human = seed.Summary.Plan.HumanName;

        foreach (FeedSourceType type in new[] { FeedSourceType.TaskAssignedToYou, FeedSourceType.TaskGateOn })
        {
            IReadOnlyList<FeedItem> items = store.Unresolved(type);
            Assert.NotEmpty(items);
            foreach (FeedItem item in items)
            {
                Assert.True(TaskId.TryParse(item.TargetKey, out TaskId id));
                TaskItem? task = tasks.Get(id);
                Assert.NotNull(task);
                Assert.True(TaskFeedRules.Holds(type, task, human, static _ => false, item.Payload), $"{item.TargetKey} no longer holds {type}");
            }
        }
    }
}
