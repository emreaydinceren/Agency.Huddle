using System.Globalization;
using System.Reflection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="TaskToolHarness.CreateAsync"/> and the members it seeds: a real
/// <see cref="TaskToolHarness.Directory"/> that already knows Nova and Kai, and a real
/// <see cref="TaskToolHarness.Triggers"/> that can preview a wake without ever being started. Also
/// proves <see cref="TaskToolHarness.Dispose"/> disposes <see cref="TaskToolHarness.Service"/>.
/// </summary>
public sealed class TaskToolHarnessTests
{
    /// <summary><see cref="TaskToolHarness.CreateAsync"/> seeds Nova and Kai so <see cref="TaskToolHarness.Directory"/> resolves them by name with the same ids it exposes.</summary>
    [Fact]
    public async Task CreateAsync_SeedsNovaAndKai_ResolvableByNameWithMatchingIds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        User? nova = harness.Directory.FindUserByName("Nova");
        User? kai = harness.Directory.FindUserByName("Kai");

        Assert.NotNull(nova);
        Assert.NotNull(kai);
        Assert.Equal(harness.NovaId, nova.Id);
        Assert.Equal(harness.KaiId, kai.Id);
    }

    /// <summary>A Task assigned to Nova, changed by the Human, previews as an unblocked wake naming Nova.</summary>
    [Fact]
    public async Task Triggers_TaskAssignedToNova_PreviewsBlockNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = TestTasks.Make(assignee: "Nova");
        TaskActor actor = TaskActors.Human(harness.Options.Value);

        WakePreview preview = harness.Triggers.Preview(null, task, actor);

        Assert.Equal("Nova", preview.AssigneeName);
        Assert.Equal(WakeBlock.None, preview.Block);
    }

    /// <summary>Disposing the harness disposes <see cref="TaskToolHarness.Service"/>, which unsubscribes it from <see cref="TaskStore.IndexChanged"/>.</summary>
    [Fact]
    public void Dispose_DisposesService_IndexChangedInvocationListDrops()
    {
        TaskToolHarness harness = new();
        FieldInfo field = typeof(TaskStore).GetField("IndexChanged", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TaskStore.IndexChanged field not found.");
        int before = ((Delegate?)field.GetValue(harness.Store))?.GetInvocationList().Length ?? 0;

        harness.Dispose();

        int after = ((Delegate?)field.GetValue(harness.Store))?.GetInvocationList().Length ?? 0;
        Assert.True(before > after);
    }

    /// <summary>
    /// <see cref="TaskToolHarness.SeedOnDisk"/> writes through <see cref="TaskStore.Create"/>, which
    /// records the file's version as already seen - so a later <see cref="TaskStore.RebuildFromWatcher"/>
    /// finds nothing changed at that path: no <see cref="TaskStore.OutsideEditDetected"/>, and the
    /// seeded Task keeps its own <see cref="TaskItem.Updated"/> and one-entry Change log, not an
    /// appended "edited outside Huddle" entry (R8 facts "Harness trap").
    /// </summary>
    [Fact]
    public void SeedOnDisk_ThenRebuild_KeepsSeededUpdated_RaisesNoOutsideEdit()
    {
        using TaskToolHarness harness = new();
        int outsideEdits = 0;
        harness.Store.OutsideEditDetected += _ => outsideEdits++;
        DateTimeOffset seededAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        TaskItem seeded = harness.SeedOnDisk(TestTasks.Make(
            id: "PLAT-0001",
            title: "Seeded",
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "Created")]));

        harness.Store.RebuildFromWatcher();

        TaskItem afterRebuild = harness.Store.Get(seeded.Id)
            ?? throw new InvalidOperationException("Seeded Task not found after rebuild.");
        Assert.Equal(0, outsideEdits);
        Assert.Equal(seededAt, afterRebuild.Updated);
        Assert.Equal(["You: Created"], afterRebuild.ChangeLog.Select(e => $"{e.Actor}: {e.Summary}").ToList());
    }

    /// <summary>
    /// A genuine edit made straight to a <see cref="TaskToolHarness.SeedOnDisk"/>-seeded file - not
    /// through <see cref="TaskService"/> - is still picked up as an outside edit on the next
    /// <see cref="TaskStore.RebuildFromWatcher"/>: <see cref="TaskToolHarness.SeedOnDisk"/> only
    /// exempts its own write, not whatever happens to the file afterwards.
    /// </summary>
    [Fact]
    public void SeedOnDisk_ThenGenuineOnDiskEdit_ThenRebuild_IsLoggedAsOutsideEdit()
    {
        using TaskToolHarness harness = new();
        TaskItem seeded = harness.SeedOnDisk(TestTasks.Make(
            id: "PLAT-0001",
            title: "Original",
            changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "Created")]));
        TaskItem editedOutside = seeded with { Title = "Edited outside" };
        File.WriteAllText(seeded.Path, TaskFileFormat.Compose(editedOutside));

        harness.Store.RebuildFromWatcher();

        TaskItem afterRebuild = harness.Store.Get(seeded.Id)
            ?? throw new InvalidOperationException("Task not found after rebuild.");
        Assert.Equal(2, afterRebuild.ChangeLog.Count);
        Assert.Equal(
            $"{harness.Options.Value.HumanName}: edited outside Huddle: title: Original → Edited outside",
            $"{afterRebuild.ChangeLog[^1].Actor}: {afterRebuild.ChangeLog[^1].Summary}");
    }
}
