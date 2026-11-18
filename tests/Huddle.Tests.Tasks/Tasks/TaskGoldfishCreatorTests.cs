using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>A Task created in a possession names the Agent as its Creator (an identity), while its Timeline keeps the goldfish stamp.</summary>
public sealed class TaskGoldfishCreatorTests
{
    /// <summary>The Creator is the plain Agent name, and the creation row of the change log still carries the stamp.</summary>
    [Fact]
    public void Create_AsAGoldfish_StoresThePlainCreator_AndAStampedLogRow()
    {
        using TaskToolHarness harness = new();
        TaskActor goldfish = new(TaskActorKind.Agent, "Ruth · goldfish", "ruth-id");

        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Fix login", "Platform", null), goldfish)).Task;

        TaskItem stored = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Missing Task.");
        Assert.Equal("Ruth", stored.Creator);
        Assert.Equal("Ruth · goldfish", Assert.Single(stored.ChangeLog).Actor);
    }
}
