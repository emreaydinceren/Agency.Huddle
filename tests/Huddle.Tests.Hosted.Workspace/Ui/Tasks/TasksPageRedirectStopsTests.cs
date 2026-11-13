using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>Pins that the Tasks page stops resolving once it has redirected a renamed route (batch 3 review): nothing is read with the old route values afterwards.</summary>
public sealed class TasksPageRedirectStopsTests
{
    /// <summary>Records every call to the wrapped directory by method name.</summary>
    public class RecordingDirectory : DispatchProxy
    {
        /// <summary>The directory calls are forwarded to.</summary>
        public ITeamDirectory? Inner { get; set; }

        /// <summary>The names of the methods called, in order.</summary>
        public List<string> Calls { get; } = [];

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            this.Calls.Add(targetMethod.Name);
            if (string.Equals(targetMethod.Name, nameof(ITeamDirectory.GetRoomsAsync), StringComparison.Ordinal))
            {
                // The harness database has no rooms table; the call is recorded and answered empty.
                return Task.FromResult<IReadOnlyList<Room>>([]);
            }

            try
            {
                return targetMethod.Invoke(this.Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }
    }

    /// <summary>After an old Team name is redirected, a View that lists Rooms (Origin) does not read the Rooms for the old route.</summary>
    [Fact]
    public async Task OldTeamName_AfterTheRedirect_ReadsNoRooms()
    {
        using TaskToolHarness harness = new();
        using TempDataDir dir = new();
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        Assert.True(harness.Views.Save(new TaskView { Id = "origin-view", Name = "Origins", Kind = ViewKind.List, Fields = ["origin"] }).Saved);
        ITeamDirectory proxy = DispatchProxy.Create<ITeamDirectory, RecordingDirectory>();
        RecordingDirectory recording = Assert.IsType<RecordingDirectory>(proxy, exactMatch: false);
        recording.Inner = harness.Directory;
        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton(proxy);
        ctx.Services.AddSingleton<ITeamCatalog>(new FakeTeamCatalog { Teams = [new TeamSummary("Growth", [], ["Nova"], true)] });
        ctx.Services.AddSingleton<ITeamRenames>(journal);

        _ = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.Team), "Marketing");
            builder.AddAttribute(3, nameof(TasksPage.ViewId), "origin-view");
            builder.CloseComponent();
        });

        Assert.DoesNotContain("GetRoomsAsync", recording.Calls);
    }
}
