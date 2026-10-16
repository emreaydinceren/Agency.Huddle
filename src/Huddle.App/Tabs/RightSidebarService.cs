using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Tabs;

/// <summary>One block of content a page has put in the Right sidebar (<c>S4</c>).</summary>
/// <param name="TabId">The Tab whose page supplied it; the sidebar shows it only while that Tab is <see cref="TabService.Active"/>.</param>
/// <param name="Title">An optional heading drawn above the content.</param>
/// <param name="Order">Where it sits among the same Tab's sections: lower first, ties in the order they were added.</param>
/// <param name="Content">What to draw. It is the page's own fragment, so its handlers run on the page.</param>
internal sealed record RightSidebarSection(string TabId, string? Title, int Order, RenderFragment Content);

/// <summary>
/// The Right sidebar of one circuit: whether it is open, and the sections each mounted page has put in
/// it. A page never talks to this directly; it renders <c>RightSidebarContent</c>, which knows its Tab
/// from the cascaded <see cref="TabPageContext"/>. Sections are kept per Tab rather than as one list
/// because two panes mount two pages at once, and the sidebar must follow the focused pane's Tab, not
/// whichever page rendered last. Scoped per circuit, and touched only on the renderer's thread.
/// </summary>
internal sealed class RightSidebarService
{
    private readonly SortedDictionary<long, RightSidebarSection> sections = [];
    private long lastId;

    /// <summary>Raised after the sidebar is opened or closed, or any section is added, changed or removed.</summary>
    public event Action? Changed;

    /// <summary>Whether the sidebar is shown. It starts closed.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Opens the sidebar when it is closed, and closes it when it is open.</summary>
    public void Toggle()
    {
        this.IsOpen = !this.IsOpen;
        this.Changed?.Invoke();
    }

    /// <summary>A new handle for one section, to pass to <see cref="Set"/> and <see cref="Remove"/>.</summary>
    public long NewId() => ++this.lastId;

    /// <summary>Adds the section with handle <paramref name="id"/>, or replaces it.</summary>
    /// <param name="id">A handle from <see cref="NewId"/>.</param>
    /// <param name="section">The section's current content.</param>
    public void Set(long id, RightSidebarSection section)
    {
        this.sections[id] = section;
        this.Changed?.Invoke();
    }

    /// <summary>Removes the section with handle <paramref name="id"/>; does nothing when there is none.</summary>
    /// <param name="id">A handle from <see cref="NewId"/>.</param>
    public void Remove(long id)
    {
        if (this.sections.Remove(id))
        {
            this.Changed?.Invoke();
        }
    }

    /// <summary>The sections the page of Tab <paramref name="tabId"/> has put in the sidebar, in the order to draw them.</summary>
    /// <param name="tabId">The Tab's id, or <see langword="null"/> when no Tab is open.</param>
    public IReadOnlyList<RightSidebarSection> SectionsFor(string? tabId) =>
        tabId is null
            ? []
            : [.. this.sections.Values.Where(section => string.Equals(section.TabId, tabId, StringComparison.Ordinal)).OrderBy(section => section.Order)];
}
