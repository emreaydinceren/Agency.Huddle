using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Registers a real <see cref="TaskStore"/> and <see cref="TaskEvents"/> for a component context whose sidebar counts open Tasks.</summary>
internal static class TaskServices
{
    /// <summary>
    /// Adds a <see cref="TaskEvents"/> and a <see cref="TaskStore"/> over a fresh <see cref="TempDataDir"/> the context
    /// owns, so a test never scans the real data folder. <paramref name="tasks"/> are written as Task files before the store
    /// first reads the folder (when something first asks for it). It needs a <see cref="PersonaStore"/> in the same context
    /// (see <c>AddPersonaStore</c>).
    /// </summary>
    /// <param name="services">The context's services.</param>
    /// <param name="tasks">The Tasks the store starts with, each filed under its own <see cref="TaskItem.Location"/>, or <see langword="null"/> for none.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTaskStore(this IServiceCollection services, IReadOnlyList<TaskItem>? tasks = null)
    {
        services.AddSingleton(new TaskEvents());
        services.AddSingleton(static _ => new OwnedTaskDir(new TempDataDir()));
        return services.AddSingleton(provider =>
        {
            TempDataDir dir = provider.GetRequiredService<OwnedTaskDir>().Dir;
            foreach (TaskItem task in tasks ?? [])
            {
                string name = task.Id + ".md";
                TestTaskStore.WriteTask(TestTaskStore.Root(dir), TestTaskStore.RelativePath(task.Location.Team, task.Location.Project, task.Location.Closed, name), task);
            }

            return TestTaskStore.CreateTaskStore(dir, provider.GetRequiredService<PersonaStore>());
        });
    }

    /// <summary>The empty data directory a context's <see cref="TaskStore"/> scans; the container disposes it.</summary>
    /// <param name="Dir">The directory.</param>
    private sealed record OwnedTaskDir(TempDataDir Dir) : IDisposable
    {
        /// <summary>Deletes the directory.</summary>
        public void Dispose() => this.Dir.Dispose();
    }
}
