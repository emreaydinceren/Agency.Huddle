using System.Globalization;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// The open Tabs of one circuit, arranged in up to <see cref="MaxGroups"/> groups drawn side by side
/// (section 5 of <c>docs/Huddle.Tabs-Specifications.md</c>). Exactly one group has focus; its active
/// Tab is <see cref="Active"/>, and the browser URL always names that Tab. Scoped per circuit. Holds
/// no UI and no <c>NavigationManager</c>: components that call it navigate to <see cref="Active"/>
/// themselves, so the same rules can be tested without a renderer.
/// </summary>
internal sealed class TabService
{
    /// <summary>
    /// The most groups that can be open at once: 2 is one split, two Tabs side by side. Raise it to allow
    /// more; nothing else assumes the number, and a stored layout with more groups than this is refused.
    /// </summary>
    public const int MaxGroups = 2;

    private readonly List<GroupState> groups = [];
    private readonly Dictionary<string, IReadOnlyList<TrailCrumb>> trails = new(StringComparer.Ordinal);
    private readonly HashSet<string> dirtyKeys = new(StringComparer.Ordinal);
    private readonly ILogger<TabService> logger;
    private IReadOnlyList<TabGroup> groupSnapshot = [];
    private IReadOnlyList<Tab> tabSnapshot = [];
    private string focusedId;
    private int lastId;
    private int lastGroupId;

    /// <summary>Starts with one empty group, which has focus.</summary>
    /// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
    public TabService(ILogger<TabService>? logger = null)
    {
        this.logger = logger ?? NullLogger<TabService>.Instance;
        GroupState first = this.NewGroup();
        this.groups.Add(first);
        this.focusedId = first.Id;
        this.RefreshSnapshots();
    }

    /// <summary>Raised after every change to <see cref="Groups"/>, <see cref="Tabs"/> or <see cref="Active"/>, and not for a call that changed nothing.</summary>
    public event Action? Changed;

    /// <summary>The groups, left to right. There is always at least one; each read returns one immutable snapshot.</summary>
    public IReadOnlyList<TabGroup> Groups => this.groupSnapshot;

    /// <summary>The id of the group that has focus.</summary>
    public string FocusedGroupId => this.focusedId;

    /// <summary>Every open Tab across every group, left to right. Each read returns one immutable snapshot.</summary>
    public IReadOnlyList<Tab> Tabs => this.tabSnapshot;

    /// <summary>The active Tab of the focused group, or <see langword="null"/> when that group is empty.</summary>
    public Tab? Active => this.Focused().ActiveTab();

    /// <summary>
    /// Opens <paramref name="url"/> and makes it active. A Tab with the same key is reused wherever it is
    /// (its URL is updated, a permanent Tab stays permanent, and its group takes focus); otherwise a
    /// preview Tab replaces the focused group's preview in place, or is inserted right of its active Tab
    /// when there is none.
    /// </summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    /// <param name="title">A better title than the fallback, such as a Room's name, or <see langword="null"/> to keep the existing or fallback one.</param>
    /// <param name="icon">An icon name, or <see langword="null"/> to use the one the URL implies.</param>
    /// <returns>The Tab now showing <paramref name="url"/>.</returns>
    public Tab Open(string url, string? title = null, string? icon = null)
    {
        string relative = TabKeys.ToRelative(url);
        TabDescriptor descriptor = TabKeys.Describe(relative);

        if (this.Find(tab => string.Equals(tab.Key, descriptor.Key, StringComparison.Ordinal)) is var (group, index))
        {
            Tab current = group.Tabs[index];
            Tab updated = current with
            {
                Url = relative,
                Title = title ?? current.Title,
                Icon = icon ?? current.Icon,
            };

            bool unchanged = updated == current
                && string.Equals(group.ActiveId, current.Id, StringComparison.Ordinal)
                && string.Equals(this.focusedId, group.Id, StringComparison.Ordinal);
            group.Tabs[index] = updated;
            group.ActiveId = updated.Id;
            this.focusedId = group.Id;
            if (!unchanged)
            {
                this.Publish();
            }

            return updated;
        }

        GroupState focused = this.Focused();
        Tab created = new(
            this.NextId(),
            descriptor.Key,
            relative,
            title ?? descriptor.Title,
            icon ?? descriptor.Icon,
            IsPreview: true);

        int preview = focused.Tabs.FindIndex(tab => tab.IsPreview);
        if (preview >= 0)
        {
            focused.Tabs[preview] = created;
        }
        else
        {
            int activeIndex = focused.Tabs.FindIndex(tab => string.Equals(tab.Id, focused.ActiveId, StringComparison.Ordinal));
            focused.Tabs.Insert(Math.Max(activeIndex + 1, focused.PinnedCount()), created);
        }

        focused.ActiveId = created.Id;
        this.Publish();
        return created;
    }

    /// <summary>Makes the Tab with <paramref name="id"/> active in its group and gives that group focus. Does nothing when it already is, or is unknown.</summary>
    /// <param name="id">The Tab to activate.</param>
    public void Activate(string id)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, _))
        {
            return;
        }

        if (string.Equals(group.ActiveId, id, StringComparison.Ordinal) && string.Equals(this.focusedId, group.Id, StringComparison.Ordinal))
        {
            return;
        }

        group.ActiveId = id;
        this.focusedId = group.Id;
        this.Publish();
    }

    /// <summary>
    /// Points the Tab with <paramref name="id"/> at <paramref name="url"/> in place, such as when a
    /// breadcrumb link is followed: the Tab keeps its group and position, takes the key, title and icon
    /// the new URL implies, becomes permanent (the navigation was deliberate), is made active and gives
    /// its group focus. When another Tab already has the new URL's key, that Tab is reused instead, as
    /// <see cref="Open(string, string?, string?)"/> reuses it, so two Tabs never share a key.
    /// </summary>
    /// <param name="id">The Tab to navigate.</param>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    /// <returns>The Tab now showing <paramref name="url"/>, or <see langword="null"/> when no Tab has <paramref name="id"/>.</returns>
    public Tab? Navigate(string id, string url)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, index))
        {
            return null;
        }

        string relative = TabKeys.ToRelative(url);
        TabDescriptor descriptor = TabKeys.Describe(relative);
        Tab current = group.Tabs[index];

        if (!string.Equals(current.Key, descriptor.Key, StringComparison.Ordinal)
            && this.Find(tab => string.Equals(tab.Key, descriptor.Key, StringComparison.Ordinal)) is not null)
        {
            return this.Open(relative);
        }

        Tab updated = string.Equals(current.Key, descriptor.Key, StringComparison.Ordinal)
            ? current with { Url = relative, IsPreview = false }
            : current with { Key = descriptor.Key, Url = relative, Title = descriptor.Title, Icon = descriptor.Icon, IsPreview = false };

        bool unchanged = updated == current
            && string.Equals(group.ActiveId, current.Id, StringComparison.Ordinal)
            && string.Equals(this.focusedId, group.Id, StringComparison.Ordinal);
        group.Tabs[index] = updated;
        group.ActiveId = updated.Id;
        this.focusedId = group.Id;
        if (!unchanged)
        {
            this.Publish();
        }

        return updated;
    }

    /// <summary>Gives the group with <paramref name="groupId"/> focus without changing its active Tab. Does nothing when it already has focus, or is unknown.</summary>
    /// <param name="groupId">The group to focus.</param>
    public void FocusGroup(string groupId)
    {
        if (string.Equals(this.focusedId, groupId, StringComparison.Ordinal) || !this.groups.Exists(group => string.Equals(group.Id, groupId, StringComparison.Ordinal)))
        {
            return;
        }

        this.focusedId = groupId;
        this.Publish();
    }

    /// <summary>Moves the active Tab of the focused group by <paramref name="offset"/> places, wrapping at either end. Does nothing when that group holds fewer than two Tabs.</summary>
    /// <param name="offset">Positive moves right, negative moves left.</param>
    /// <returns>The Tab that is now active, or <see langword="null"/> when nothing changed.</returns>
    public Tab? ActivateRelative(int offset)
    {
        GroupState focused = this.Focused();
        int current = focused.Tabs.FindIndex(tab => string.Equals(tab.Id, focused.ActiveId, StringComparison.Ordinal));
        if (focused.Tabs.Count < 2 || current < 0)
        {
            return null;
        }

        int target = (((current + offset) % focused.Tabs.Count) + focused.Tabs.Count) % focused.Tabs.Count;
        focused.ActiveId = focused.Tabs[target].Id;
        this.Publish();
        return focused.Tabs[target];
    }

    /// <summary>Makes the Tab with <paramref name="id"/> permanent. Does nothing when it already is or is unknown.</summary>
    /// <param name="id">The Tab to keep.</param>
    public void Keep(string id)
    {
        this.Replace(tab => string.Equals(tab.Id, id, StringComparison.Ordinal), tab => tab with { IsPreview = false });
    }

    /// <summary>Makes the Tab with <paramref name="key"/> permanent. Does nothing when it already is or none has that key.</summary>
    /// <param name="key">The key of the Tab to keep, as <see cref="TabKeys.Describe(string)"/> gives it.</param>
    public void KeepByKey(string key)
    {
        this.Replace(tab => string.Equals(tab.Key, key, StringComparison.Ordinal), tab => tab with { IsPreview = false });
    }

    /// <summary>Pins the Tab with <paramref name="id"/>: it becomes permanent and moves to the end of its group's pinned Tabs. Does nothing when it is pinned or unknown.</summary>
    /// <param name="id">The Tab to pin.</param>
    public void Pin(string id)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, index) || group.Tabs[index].IsPinned)
        {
            return;
        }

        Tab pinned = group.Tabs[index] with { IsPinned = true, IsPreview = false };
        group.Tabs.RemoveAt(index);
        group.Tabs.Insert(group.PinnedCount(), pinned);
        this.Publish();
    }

    /// <summary>Unpins the Tab with <paramref name="id"/>: it stays permanent and moves to just after its group's pinned Tabs. Does nothing when it is not pinned or unknown.</summary>
    /// <param name="id">The Tab to unpin.</param>
    public void Unpin(string id)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, index) || !group.Tabs[index].IsPinned)
        {
            return;
        }

        Tab unpinned = group.Tabs[index] with { IsPinned = false };
        group.Tabs.RemoveAt(index);
        group.Tabs.Insert(group.PinnedCount(), unpinned);
        this.Publish();
    }

    /// <summary>Changes the title of the Tab with <paramref name="key"/>, such as when a Room is renamed. Does nothing when none has that key.</summary>
    /// <param name="key">The key of the Tab to retitle.</param>
    /// <param name="title">The new title.</param>
    public void SetTitle(string key, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        this.Replace(tab => string.Equals(tab.Key, key, StringComparison.Ordinal), tab => tab with { Title = title });
    }

    /// <summary>
    /// Records whether the page in the Tab with <paramref name="key"/> holds edits that closing the Tab would lose. The Tab strip and the
    /// close shortcut ask the Human before they close a Tab for which this is <see langword="true"/>. A page sets it while it has pending
    /// edits and clears it when they are saved, discarded or the page goes away. It changes no Tab and raises no event.
    /// </summary>
    /// <param name="key">The key of the Tab whose page holds the edits.</param>
    /// <param name="dirty">Whether the page has unsaved edits.</param>
    public void SetDirty(string key, bool dirty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _ = dirty ? this.dirtyKeys.Add(key) : this.dirtyKeys.Remove(key);
    }

    /// <summary>Whether the page in the Tab with <paramref name="key"/> has unsaved edits (<see cref="SetDirty(string, bool)"/>).</summary>
    /// <param name="key">The key of the Tab to ask about.</param>
    public bool IsDirty(string key) => this.dirtyKeys.Contains(key);

    /// <summary>
    /// Replaces the breadcrumb trail <see cref="Trail(Tab)"/> gives for the Tab with <paramref name="key"/>,
    /// for a page whose location changes inside one URL, such as the Library's open file. The trail is
    /// not stored with the Tab, so a restored layout starts from the URL's own trail until the page
    /// sets it again.
    /// </summary>
    /// <param name="key">The key of the Tab whose trail the page owns.</param>
    /// <param name="crumbs">The trail, or <see langword="null"/> to go back to the one the URL implies.</param>
    public void SetTrail(string key, IReadOnlyList<TrailCrumb>? crumbs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        bool had = this.trails.TryGetValue(key, out IReadOnlyList<TrailCrumb>? current);
        if (crumbs is null ? !had : had && current is not null && current.SequenceEqual(crumbs))
        {
            return;
        }

        if (crumbs is null)
        {
            _ = this.trails.Remove(key);
        }
        else
        {
            this.trails[key] = crumbs;
        }

        this.Publish();
    }

    /// <summary>The breadcrumb trail of <paramref name="tab"/>: the one its page set with <see cref="SetTrail(string, IReadOnlyList{TrailCrumb}?)"/>, else the one its URL implies.</summary>
    /// <param name="tab">The Tab to draw the trail for.</param>
    public IReadOnlyList<TrailCrumb> Trail(Tab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return this.trails.TryGetValue(tab.Key, out IReadOnlyList<TrailCrumb>? crumbs) ? crumbs : TabTrail.Build(tab.Url);
    }

    /// <summary>Whether <see cref="MoveLeft(string)"/> would do something for the Tab with <paramref name="id"/>.</summary>
    /// <param name="id">The Tab to ask about.</param>
    public bool CanMoveLeft(string id) => this.CanMove(id, -1);

    /// <summary>Whether <see cref="MoveRight(string)"/> would do something for the Tab with <paramref name="id"/>.</summary>
    /// <param name="id">The Tab to ask about.</param>
    public bool CanMoveRight(string id) => this.CanMove(id, 1);

    /// <summary>
    /// Moves the Tab with <paramref name="id"/> into the group on its left, which then has focus with the
    /// Tab active. From the leftmost group it opens a new group on the left, if fewer than
    /// <see cref="MaxGroups"/> exist and the Tab has company. A moved Tab is made permanent.
    /// </summary>
    /// <param name="id">The Tab to move.</param>
    /// <returns><see langword="true"/> when the Tab moved.</returns>
    public bool MoveLeft(string id) => this.Move(id, -1);

    /// <summary>The mirror of <see cref="MoveLeft(string)"/>: into the group on the right, or a new one past the rightmost.</summary>
    /// <param name="id">The Tab to move.</param>
    /// <returns><see langword="true"/> when the Tab moved.</returns>
    public bool MoveRight(string id) => this.Move(id, 1);

    /// <summary>
    /// Closes the Tab with <paramref name="id"/>. When it was active, its right neighbour in the group
    /// becomes active, else its left one, else the group is empty. An emptied group goes away unless it
    /// is the only one, and focus then moves to a group that remains.
    /// </summary>
    /// <param name="id">The Tab to close.</param>
    public void Close(string id)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, index))
        {
            return;
        }

        this.RemoveFrom(group, index);
        this.Publish();
    }

    /// <summary>Closes the Tab with <paramref name="key"/>, such as when its Room is archived or deleted. Does nothing when none has that key.</summary>
    /// <param name="key">The key of the Tab to close.</param>
    public void CloseByKey(string key)
    {
        if (this.Find(tab => string.Equals(tab.Key, key, StringComparison.Ordinal)) is var (group, index))
        {
            this.Close(group.Tabs[index].Id);
        }
    }

    /// <summary>Captures the layout and the Tabs for storage.</summary>
    public TabSnapshot Export()
    {
        List<TabGroupSnapshot> exported = [.. this.groups.Select(static group => new TabGroupSnapshot(
            TabGroupSnapshot.GroupKind,
            group.ActiveId,
            [.. group.Tabs.Select(static tab => new TabSnapshotEntry(tab.Id, tab.Url, tab.Title, tab.Icon, tab.IsPreview, tab.IsPinned))]))];

        TabGroupSnapshot root = exported.Count == 1
            ? exported[0]
            : new TabGroupSnapshot(
                TabGroupSnapshot.SplitKind,
                null,
                [],
                exported,
                this.FocusedIndex());

        return new TabSnapshot(TabSnapshot.CurrentVersion, root);
    }

    /// <summary>
    /// Replaces every Tab and group with those in <paramref name="stored"/>, after checking them. A
    /// snapshot that is missing, of an unknown version, or holds an unsafe URL, a duplicate id or key, a
    /// pinned Tab after an unpinned one, more than one preview in a group, an empty group in a split, or
    /// more groups than <see cref="MaxGroups"/> is refused whole and leaves the Tabs as they were.
    /// </summary>
    /// <param name="stored">A snapshot read back from storage, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the snapshot was accepted.</returns>
    public bool Restore(TabSnapshot? stored)
    {
        if (stored is not { Root: { } root } || !TabSnapshot.ReadableVersions.Contains(stored.Version))
        {
            return false;
        }

        IReadOnlyList<TabGroupSnapshot> nodes;
        if (string.Equals(root.Kind, TabGroupSnapshot.GroupKind, StringComparison.Ordinal))
        {
            nodes = [root];
        }
        else if (string.Equals(root.Kind, TabGroupSnapshot.SplitKind, StringComparison.Ordinal)
            && root.Children is { Count: >= 2 } children
            && children.Count <= MaxGroups
            && children.All(static child => string.Equals(child.Kind, TabGroupSnapshot.GroupKind, StringComparison.Ordinal) && child.Tabs is { Count: > 0 }))
        {
            nodes = children;
        }
        else
        {
            return false;
        }

        List<(string? ActiveId, List<Tab> Tabs)> restored = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (TabGroupSnapshot node in nodes)
        {
            if (node.Tabs is not { } entries || !TryRestoreGroup(entries, ids, keys, out List<Tab>? tabs))
            {
                return false;
            }

            restored.Add((node.ActiveId, tabs));
        }

        this.groups.Clear();
        this.lastGroupId = 0;
        foreach ((string? activeId, List<Tab> tabs) in restored)
        {
            GroupState group = this.NewGroup();
            group.Tabs.AddRange(tabs);
            group.ActiveId = tabs.Exists(tab => string.Equals(tab.Id, activeId, StringComparison.Ordinal)) ? activeId : tabs.FirstOrDefault()?.Id;
            this.groups.Add(group);
        }

        int focused = nodes.Count == 1 ? 0 : Math.Clamp(root.Focused, 0, this.groups.Count - 1);
        this.focusedId = this.groups[focused].Id;
        this.lastId = this.groups.SelectMany(static group => group.Tabs).Select(static tab => ParseIdNumber(tab.Id)).DefaultIfEmpty(0).Max();
        this.Publish();
        return true;
    }

    private static bool TryRestoreGroup(IReadOnlyList<TabSnapshotEntry> entries, HashSet<string> ids, HashSet<string> keys, out List<Tab> tabs)
    {
        tabs = [];
        bool sawUnpinned = false;
        foreach (TabSnapshotEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Title)
                || !TabKeys.IsSafeRelative(entry.Url))
            {
                return false;
            }

            string key = TabKeys.Describe(entry.Url).Key;
            if (!ids.Add(entry.Id) || !keys.Add(key) || (entry.Pinned && (entry.Preview || sawUnpinned)))
            {
                return false;
            }

            sawUnpinned |= !entry.Pinned;
            tabs.Add(new Tab(entry.Id, key, entry.Url, entry.Title, string.IsNullOrWhiteSpace(entry.Icon) ? "page" : entry.Icon, entry.Preview, entry.Pinned));
        }

        return tabs.Count(static tab => tab.IsPreview) <= 1;
    }

    private static int ParseIdNumber(string id) =>
        id.Length > 1 && id[0] == 't' && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;

    private GroupState Focused()
    {
        int index = this.FocusedIndex();
        return this.groups[index < 0 ? 0 : index];
    }

    /// <summary>The index of the focused group, or -1 when none matches; a plain loop, so the render path allocates no closure.</summary>
    private int FocusedIndex()
    {
        for (int index = 0; index < this.groups.Count; index++)
        {
            if (string.Equals(this.groups[index].Id, this.focusedId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private (GroupState Group, int Index)? Find(Predicate<Tab> match)
    {
        foreach (GroupState group in this.groups)
        {
            int index = group.Tabs.FindIndex(match);
            if (index >= 0)
            {
                return (group, index);
            }
        }

        return null;
    }

    private void Replace(Predicate<Tab> match, Func<Tab, Tab> change)
    {
        if (this.Find(match) is not var (group, index))
        {
            return;
        }

        Tab updated = change(group.Tabs[index]);
        if (updated == group.Tabs[index])
        {
            return;
        }

        group.Tabs[index] = updated;
        this.Publish();
    }

    private bool CanMove(string id, int direction)
    {
        if (this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (group, _))
        {
            return false;
        }

        int target = this.groups.IndexOf(group) + direction;
        return (target >= 0 && target < this.groups.Count)
            || (this.groups.Count < MaxGroups && group.Tabs.Count > 1);
    }

    private bool Move(string id, int direction)
    {
        if (!this.CanMove(id, direction) || this.Find(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)) is not var (source, index))
        {
            return false;
        }

        Tab moved = source.Tabs[index] with { IsPreview = false };
        int targetIndex = this.groups.IndexOf(source) + direction;
        GroupState target;
        if (targetIndex >= 0 && targetIndex < this.groups.Count)
        {
            target = this.groups[targetIndex];
        }
        else
        {
            target = this.NewGroup();
            this.groups.Insert(direction < 0 ? 0 : this.groups.Count, target);
        }

        source.Tabs.RemoveAt(index);
        if (string.Equals(source.ActiveId, id, StringComparison.Ordinal))
        {
            source.ActiveId = source.Tabs.Count == 0 ? null : source.Tabs[Math.Min(index, source.Tabs.Count - 1)].Id;
        }

        target.Tabs.Insert(moved.IsPinned ? target.PinnedCount() : target.Tabs.Count, moved);
        target.ActiveId = moved.Id;
        this.focusedId = target.Id;
        if (source.Tabs.Count == 0)
        {
            this.groups.Remove(source);
        }

        this.Publish();
        return true;
    }

    private void RemoveFrom(GroupState group, int index)
    {
        bool wasActive = string.Equals(group.ActiveId, group.Tabs[index].Id, StringComparison.Ordinal);
        group.Tabs.RemoveAt(index);
        if (wasActive)
        {
            group.ActiveId = group.Tabs.Count == 0 ? null : group.Tabs[Math.Min(index, group.Tabs.Count - 1)].Id;
        }

        if (group.Tabs.Count > 0 || this.groups.Count == 1)
        {
            return;
        }

        int position = this.groups.IndexOf(group);
        this.groups.RemoveAt(position);
        if (string.Equals(this.focusedId, group.Id, StringComparison.Ordinal))
        {
            this.focusedId = this.groups[Math.Min(position, this.groups.Count - 1)].Id;
        }
    }

    private GroupState NewGroup()
    {
        this.lastGroupId++;
        return new GroupState(string.Create(CultureInfo.InvariantCulture, $"g{this.lastGroupId}"));
    }

    private string NextId()
    {
        this.lastId++;
        return string.Create(CultureInfo.InvariantCulture, $"t{this.lastId}");
    }

    private void RefreshSnapshots()
    {
        this.groupSnapshot = [.. this.groups.Select(static group => new TabGroup(group.Id, [.. group.Tabs], group.ActiveId))];
        this.tabSnapshot = [.. this.groups.SelectMany(static group => group.Tabs)];
    }

    private void Publish()
    {
        this.RefreshSnapshots();
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>The mutable state of one group; every outside view is an immutable <see cref="TabGroup"/>.</summary>
    private sealed class GroupState(string id)
    {
        public string Id { get; } = id;

        public List<Tab> Tabs { get; } = [];

        public string? ActiveId { get; set; }

        public Tab? ActiveTab()
        {
            if (this.ActiveId is null)
            {
                return null;
            }

            foreach (Tab tab in this.Tabs)
            {
                if (string.Equals(tab.Id, this.ActiveId, StringComparison.Ordinal))
                {
                    return tab;
                }
            }

            return null;
        }

        public int PinnedCount()
        {
            int count = 0;
            while (count < this.Tabs.Count && this.Tabs[count].IsPinned)
            {
                count++;
            }

            return count;
        }
    }
}
