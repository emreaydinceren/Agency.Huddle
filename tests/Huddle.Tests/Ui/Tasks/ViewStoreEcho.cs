using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Saves a View through a <see cref="ViewStore"/> and waits out the store's own watcher echo, so a page or drawer a
/// test renders afterwards is not re-rendered half a second into the test.
/// </summary>
/// <remarks>
/// <see cref="ViewStore"/> has no own-write suppression: <see cref="ViewStore.Save(TaskView)"/> raises
/// <see cref="ViewStore.ViewsChanged"/> once itself, and its <c>FileSystemWatcher</c> raises it a second time about half
/// a second later. A component subscribed to the event re-renders on that second raise, which retires the event-handler
/// ids of elements the test has already found. The echo is an event, so this helper awaits it rather than sleeping.
/// </remarks>
internal static class ViewStoreEcho
{
    /// <summary>Saves <paramref name="view"/> and returns once the watcher's redundant <see cref="ViewStore.ViewsChanged"/> has been raised.</summary>
    /// <param name="views">The store to save into.</param>
    /// <param name="view">The View to save.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>The save's result.</returns>
    internal static async Task<ViewSaveResult> SaveAndSettleAsync(ViewStore views, TaskView view, CancellationToken ct)
    {
        TaskCompletionSource echoed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int raised = 0;
        void OnViewsChanged()
        {
            if (Interlocked.Increment(ref raised) == 2)
            {
                echoed.TrySetResult();
            }
        }

        views.ViewsChanged += OnViewsChanged;
        try
        {
            ViewSaveResult result = views.Save(view);
            if (result.Saved)
            {
                await echoed.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }

            return result;
        }
        finally
        {
            views.ViewsChanged -= OnViewsChanged;
        }
    }
}
