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
}
