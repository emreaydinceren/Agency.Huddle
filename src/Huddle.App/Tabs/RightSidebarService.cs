using Agency.Huddle.App.Sidebar;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// The Right sidebar of one circuit: whether it is open, and the Subject each mounted page has published
/// (Spec D-2). A page never talks to this directly; it renders <c>PageSubjectSource</c>, which knows its
/// Tab from the cascaded <see cref="TabPageContext"/>. Subjects are kept per Tab because two panes mount
/// two pages at once, and the sidebar must follow the focused pane's Tab. Scoped per circuit, and touched
/// only on the renderer's thread.
/// </summary>
internal sealed class RightSidebarService
{
    private readonly Dictionary<long, (string TabId, PageSubject Subject, long Seq)> entries = [];
    private long lastId;
    private long lastSeq;

    /// <summary>Raised after the sidebar is opened or closed, or any Subject is added, changed or removed.</summary>
    public event Action? Changed;

    /// <summary>Whether the sidebar is shown. It starts closed.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>What the sidebar shows while open. <see cref="RightSidebarMode.Subject"/> unless the Notifications button opened it.</summary>
    public RightSidebarMode Mode { get; private set; }

    /// <summary>Opens the sidebar when it is closed, and closes it when it is open. Always in <see cref="RightSidebarMode.Subject"/> mode.</summary>
    public void Toggle()
    {
        this.IsOpen = !this.IsOpen;
        this.Mode = RightSidebarMode.Subject;
        this.Changed?.Invoke();
    }

    /// <summary>Opens the sidebar on the active Tab's Subject, whatever it showed before: a page calls this when the Human asks for something the sidebar draws, such as a Task's details. Does nothing when it already shows a Subject.</summary>
    public void ShowSubject()
    {
        if (this.IsOpen && this.Mode == RightSidebarMode.Subject)
        {
            return;
        }

        this.IsOpen = true;
        this.Mode = RightSidebarMode.Subject;
        this.Changed?.Invoke();
    }

    /// <summary>
    /// The Notifications button: opens the sidebar in <see cref="RightSidebarMode.Notifications"/> mode when it is closed or showing
    /// a Subject, and closes it when it is already showing Notifications.
    /// </summary>
    public void ToggleNotifications()
    {
        bool showing = this.IsOpen && this.Mode == RightSidebarMode.Notifications;
        this.IsOpen = !showing;
        this.Mode = showing ? RightSidebarMode.Subject : RightSidebarMode.Notifications;
        this.Changed?.Invoke();
    }

    /// <summary>A new handle for one source, to pass to <see cref="Set"/> and <see cref="Remove"/>.</summary>
    public long NewId() => ++this.lastId;

    /// <summary>Publishes <paramref name="subject"/> for Tab <paramref name="tabId"/> under handle <paramref name="id"/>; an equal repeat does nothing and raises nothing.</summary>
    /// <param name="id">A handle from <see cref="NewId"/>.</param>
    /// <param name="tabId">The Tab whose page publishes it.</param>
    /// <param name="subject">What the page is showing now.</param>
    public void Set(long id, string tabId, PageSubject subject)
    {
        if (this.entries.TryGetValue(id, out (string TabId, PageSubject Subject, long Seq) existing)
            && string.Equals(existing.TabId, tabId, StringComparison.Ordinal)
            && existing.Subject == subject)
        {
            return;
        }

        this.entries[id] = (tabId, subject, ++this.lastSeq);
        this.Changed?.Invoke();
    }

    /// <summary>Removes the source with handle <paramref name="id"/>; does nothing when there is none.</summary>
    /// <param name="id">A handle from <see cref="NewId"/>.</param>
    public void Remove(long id)
    {
        if (this.entries.Remove(id))
        {
            this.Changed?.Invoke();
        }
    }

    /// <summary>The Subject most recently set for Tab <paramref name="tabId"/>, or null when there is none.</summary>
    /// <param name="tabId">The Tab's id, or <see langword="null"/> when no Tab is open.</param>
    public PageSubject? SubjectFor(string? tabId)
    {
        if (tabId is null)
        {
            return null;
        }

        PageSubject? best = null;
        long bestSeq = -1;
        foreach ((string entryTab, PageSubject subject, long seq) in this.entries.Values)
        {
            if (seq > bestSeq && string.Equals(entryTab, tabId, StringComparison.Ordinal))
            {
                best = subject;
                bestSeq = seq;
            }
        }

        return best;
    }
}
