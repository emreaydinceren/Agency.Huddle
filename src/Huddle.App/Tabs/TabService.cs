using System.Globalization;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// The open Tabs of one circuit, in one group (version 1 has exactly one; see section 5 of
/// <c>docs/Huddle.Tabs-Specifications.md</c>). Scoped per circuit. Holds no UI and no
/// <c>NavigationManager</c>: components that call it navigate to <see cref="Active"/> themselves, so
/// the same rules can be tested without a renderer.
/// </summary>
internal sealed class TabService
{
    private readonly List<Tab> tabs = [];
    private IReadOnlyList<Tab> snapshot = [];
    private string? activeId;
    private int lastId;

    /// <summary>Raised after every change to <see cref="Tabs"/> or <see cref="Active"/>, and not for a call that changed nothing.</summary>
    public event Action? Changed;

    /// <summary>The open Tabs, left to right. Each read returns one immutable snapshot.</summary>
    public IReadOnlyList<Tab> Tabs => this.snapshot;

    /// <summary>The active Tab, or <see langword="null"/> when none is open.</summary>
    public Tab? Active => this.activeId is null ? null : this.tabs.Find(tab => string.Equals(tab.Id, this.activeId, StringComparison.Ordinal));

    /// <summary>
    /// Opens <paramref name="url"/> and makes it active. A Tab with the same key is reused (its URL is
    /// updated, a permanent Tab stays permanent); otherwise a preview Tab replaces the existing preview
    /// in place, or is inserted right of the active Tab when there is none.
    /// </summary>
    /// <param name="url">An absolute URI or a site-relative address.</param>
    /// <param name="title">A better title than the fallback, such as a Room's name, or <see langword="null"/> to keep the existing or fallback one.</param>
    /// <param name="icon">An icon name, or <see langword="null"/> to use the one the URL implies.</param>
    /// <returns>The Tab now showing <paramref name="url"/>.</returns>
    public Tab Open(string url, string? title = null, string? icon = null)
    {
        string relative = TabKeys.ToRelative(url);
        TabDescriptor descriptor = TabKeys.Describe(relative);

        int existing = this.tabs.FindIndex(tab => string.Equals(tab.Key, descriptor.Key, StringComparison.Ordinal));
        if (existing >= 0)
        {
            Tab current = this.tabs[existing];
            Tab updated = current with
            {
                Url = relative,
                Title = title ?? current.Title,
                Icon = icon ?? current.Icon,
            };

            bool unchanged = updated == current && string.Equals(this.activeId, current.Id, StringComparison.Ordinal);
            this.tabs[existing] = updated;
            this.activeId = updated.Id;
            if (!unchanged)
            {
                this.Publish();
            }

            return updated;
        }

        Tab created = new(
            this.NextId(),
            descriptor.Key,
            relative,
            title ?? descriptor.Title,
            icon ?? descriptor.Icon,
            IsPreview: true);

        int preview = this.tabs.FindIndex(tab => tab.IsPreview);
        if (preview >= 0)
        {
            this.tabs[preview] = created;
        }
        else
        {
            int activeIndex = this.tabs.FindIndex(tab => string.Equals(tab.Id, this.activeId, StringComparison.Ordinal));
            this.tabs.Insert(activeIndex + 1, created);
        }

        this.activeId = created.Id;
        this.Publish();
        return created;
    }

    /// <summary>Makes the Tab with <paramref name="id"/> active. Does nothing when it is already active or unknown.</summary>
    /// <param name="id">The Tab to activate.</param>
    public void Activate(string id)
    {
        if (string.Equals(this.activeId, id, StringComparison.Ordinal) || !this.tabs.Exists(tab => string.Equals(tab.Id, id, StringComparison.Ordinal)))
        {
            return;
        }

        this.activeId = id;
        this.Publish();
    }

    /// <summary>Makes the Tab with <paramref name="id"/> permanent. Does nothing when it already is or is unknown.</summary>
    /// <param name="id">The Tab to keep.</param>
    public void Pin(string id)
    {
        this.Replace(tab => string.Equals(tab.Id, id, StringComparison.Ordinal), tab => tab with { IsPreview = false });
    }

    /// <summary>Makes the Tab with <paramref name="key"/> permanent. Does nothing when it already is or none has that key.</summary>
    /// <param name="key">The key of the Tab to keep, as <see cref="TabKeys.Describe(string)"/> gives it.</param>
    public void PinByKey(string key)
    {
        this.Replace(tab => string.Equals(tab.Key, key, StringComparison.Ordinal), tab => tab with { IsPreview = false });
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
    /// Closes the Tab with <paramref name="id"/>. When it was active, its right neighbour becomes
    /// active, else its left one, else nothing is.
    /// </summary>
    /// <param name="id">The Tab to close.</param>
    public void Close(string id)
    {
        int index = this.tabs.FindIndex(tab => string.Equals(tab.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        bool wasActive = string.Equals(this.activeId, id, StringComparison.Ordinal);
        this.tabs.RemoveAt(index);
        if (wasActive)
        {
            this.activeId = this.tabs.Count == 0 ? null : this.tabs[Math.Min(index, this.tabs.Count - 1)].Id;
        }

        this.Publish();
    }

    /// <summary>Closes the Tab with <paramref name="key"/>, such as when its Room is archived or deleted. Does nothing when none has that key.</summary>
    /// <param name="key">The key of the Tab to close.</param>
    public void CloseByKey(string key)
    {
        Tab? tab = this.tabs.Find(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        if (tab is not null)
        {
            this.Close(tab.Id);
        }
    }

    /// <summary>Captures the Tabs for storage.</summary>
    public TabSnapshot Export() => new(
        TabSnapshot.CurrentVersion,
        new TabGroupSnapshot(
            TabGroupSnapshot.GroupKind,
            this.activeId,
            [.. this.tabs.Select(static tab => new TabSnapshotEntry(tab.Id, tab.Url, tab.Title, tab.Icon, tab.IsPreview))]));

    /// <summary>
    /// Replaces every Tab with those in <paramref name="stored"/>, after checking them. A snapshot that
    /// is missing, of another version, or holds an unsafe URL, a duplicate, or more than one preview is
    /// refused whole and leaves the Tabs as they were.
    /// </summary>
    /// <param name="stored">A snapshot read back from storage, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the snapshot was accepted.</returns>
    public bool Restore(TabSnapshot? stored)
    {
        if (stored is not { Version: TabSnapshot.CurrentVersion, Root: { Kind: TabGroupSnapshot.GroupKind, Tabs: { } entries } root })
        {
            return false;
        }

        List<Tab> restored = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (TabSnapshotEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Title)
                || !TabKeys.IsSafeRelative(entry.Url))
            {
                return false;
            }

            string key = TabKeys.Describe(entry.Url).Key;
            if (!ids.Add(entry.Id) || !keys.Add(key))
            {
                return false;
            }

            restored.Add(new Tab(entry.Id, key, entry.Url, entry.Title, string.IsNullOrWhiteSpace(entry.Icon) ? "page" : entry.Icon, entry.Preview));
        }

        if (restored.Count(static tab => tab.IsPreview) > 1)
        {
            return false;
        }

        this.tabs.Clear();
        this.tabs.AddRange(restored);
        this.activeId = restored.Exists(tab => string.Equals(tab.Id, root.ActiveId, StringComparison.Ordinal))
            ? root.ActiveId
            : restored.FirstOrDefault()?.Id;
        this.lastId = restored.Select(static tab => ParseIdNumber(tab.Id)).DefaultIfEmpty(0).Max();
        this.Publish();
        return true;
    }

    private void Replace(Func<Tab, bool> match, Func<Tab, Tab> change)
    {
        int index = this.tabs.FindIndex(tab => match(tab));
        if (index < 0)
        {
            return;
        }

        Tab updated = change(this.tabs[index]);
        if (updated == this.tabs[index])
        {
            return;
        }

        this.tabs[index] = updated;
        this.Publish();
    }

    private string NextId()
    {
        this.lastId++;
        return string.Create(CultureInfo.InvariantCulture, $"t{this.lastId}");
    }

    private static int ParseIdNumber(string id) =>
        id.Length > 1 && id[0] == 't' && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;

    private void Publish()
    {
        this.snapshot = [.. this.tabs];
        this.Changed?.Invoke();
    }
}
