namespace Agency.Huddle.App.Sidebar;

/// <summary>
/// Carries <see cref="PageCommand"/>s from a sidebar panel to the page of its Subject, addressed by
/// Tab id (Spec D-6). Commands are neither stored nor replayed, and a command nobody handles is a no-op.
/// </summary>
internal sealed class PageCommandBus
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<Func<PageCommand, Task>>> handlers = new(StringComparer.Ordinal);

    /// <summary>Starts delivering the Tab's commands to <paramref name="handler"/>.</summary>
    /// <param name="tabId">The Tab whose commands to receive.</param>
    /// <param name="handler">Called once per command, in subscription order.</param>
    /// <returns>A subscription that removes exactly this handler when disposed.</returns>
    internal IDisposable Subscribe(string tabId, Func<PageCommand, Task> handler)
    {
        lock (this.gate)
        {
            if (!this.handlers.TryGetValue(tabId, out List<Func<PageCommand, Task>>? list))
            {
                list = [];
                this.handlers[tabId] = list;
            }

            list.Add(handler);
        }

        return new Subscription(this, tabId, handler);
    }

    /// <summary>Delivers a command to every handler of the Tab, awaiting each in subscription order.</summary>
    /// <param name="tabId">The Tab the command is for.</param>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when every handler has finished.</returns>
    internal async Task SendAsync(string tabId, PageCommand command)
    {
        Func<PageCommand, Task>[] snapshot;
        lock (this.gate)
        {
            snapshot = this.handlers.TryGetValue(tabId, out List<Func<PageCommand, Task>>? list) ? [.. list] : [];
        }

        foreach (Func<PageCommand, Task> handler in snapshot)
        {
            await handler(command).ConfigureAwait(false);
        }
    }

    private void Unsubscribe(string tabId, Func<PageCommand, Task> handler)
    {
        lock (this.gate)
        {
            if (this.handlers.TryGetValue(tabId, out List<Func<PageCommand, Task>>? list))
            {
                _ = list.Remove(handler);
                if (list.Count == 0)
                {
                    _ = this.handlers.Remove(tabId);
                }
            }
        }
    }

    /// <summary>One handler's registration; disposing it removes that handler.</summary>
    private sealed class Subscription(PageCommandBus bus, string tabId, Func<PageCommand, Task> handler) : IDisposable
    {
        /// <summary>Removes the handler from the bus.</summary>
        public void Dispose()
        {
            bus.Unsubscribe(tabId, handler);
        }
    }
}
