using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Components.Feed;

/// <summary>
/// The paged, live list behind both the Feed Tab and the Notifications panel: what is loaded, where the next page starts, which rows
/// were resolved or dismissed since they were drawn, and which items that arrive live belong in the list (Feed spec §6.8, §6.9).
/// A plain object, so the two components keep only their own concerns (counts, the seen marks, the header) and the paging rules live once.
/// </summary>
/// <param name="store">The Feed store the pages are read from.</param>
/// <param name="pageSize">Items per page, also the smallest reload.</param>
/// <param name="hideDismissed">Whether Notifications the Human dismissed are left out of the Needs you list (the panel does; the Tab does not).</param>
internal sealed class FeedPager(FeedStore store, int pageSize, bool hideDismissed = false)
{
    private readonly HashSet<long> hidden = [];
    private readonly Dictionary<long, (bool Resolved, bool Dismissed)> known = [];
    private int loadedCount;
    private long lowestLoadedId;
    private long highestLoadedId;

    /// <summary>The Category shown.</summary>
    public FeedCategory Category { get; set; } = FeedCategory.All;

    /// <summary>Only this Agent's items, or <see langword="null"/> for everyone's.</summary>
    public string? Agent { get; set; }

    /// <summary>Only this Team's items, or <see langword="null"/> for every Team.</summary>
    public string? Team { get; set; }

    /// <summary>The items loaded so far, newest first.</summary>
    public IReadOnlyList<FeedItem> Items { get; private set; } = [];

    /// <summary>Whether the last page was full, so "Show more" may find older items.</summary>
    public bool HasMore { get; private set; }

    /// <summary>A key that changes with every fresh load, so the list below remounts instead of patching itself.</summary>
    public int ListKey { get; private set; }

    /// <summary>Whether live items arrived since the last load, which keeps the empty state from showing over a waiting pill.</summary>
    public bool Arrived { get; private set; }

    /// <summary>The newest item id any load has returned. Safe to read from another thread.</summary>
    public long HighestLoadedId => Volatile.Read(ref this.highestLoadedId);

    /// <summary>Reads the first page and starts a fresh list.</summary>
    public void Load() => this.Load(pageSize);

    /// <summary>Reads the next older page and appends it.</summary>
    public void ShowMore()
    {
        IReadOnlyList<FeedItem> page = store.Page(this.QueryFor(this.lowestLoadedId, pageSize));
        List<FeedItem> older = this.Visible(page);
        this.Items = [.. this.Items, .. older];
        this.loadedCount += page.Count;
        this.lowestLoadedId = page.Count > 0 ? page[^1].Id : this.lowestLoadedId;
        this.HasMore = page.Count >= pageSize;
        foreach (FeedItem item in older)
        {
            this.Remember(item);
        }
    }

    /// <summary>Takes the items of <paramref name="added"/> that belong in the list and are not already in it.</summary>
    /// <param name="added">What the writer just inserted.</param>
    /// <returns>The items to hand to the list, newest first; empty when none belong.</returns>
    public IReadOnlyList<FeedItem> Take(IReadOnlyList<FeedItem> added)
    {
        ArgumentNullException.ThrowIfNull(added);

        List<FeedItem> matching = [.. added.Where(item => this.Matches(item) && !this.known.ContainsKey(item.Id))];
        if (matching.Count > 0)
        {
            this.Arrived = true;
            this.loadedCount += matching.Count;
            foreach (FeedItem item in matching)
            {
                this.Remember(item);
            }
        }

        return matching;
    }

    /// <summary>
    /// Brings the rows already drawn up to date without a new list: a row whose resolved or dismissed state changed is replaced by its new
    /// version in <see cref="Items"/>, and in the Needs you view a row that left the query is removed. <see cref="ListKey"/> stays, so the
    /// list keeps its expanded card and its waiting new items.
    /// </summary>
    /// <returns>What changed, for the list to patch the rows it holds itself.</returns>
    public FeedRefresh RefreshInPlace()
    {
        Dictionary<long, FeedItem> current = store.Page(this.QueryFor(null, Math.Max(pageSize, this.loadedCount))).ToDictionary(static item => item.Id);
        List<FeedItem> changed = [];
        HashSet<long> removed = [];
        foreach ((long id, (bool Resolved, bool Dismissed) before) in this.known.ToList())
        {
            if (!current.TryGetValue(id, out FeedItem? now))
            {
                if (this.Category == FeedCategory.NeedsYou)
                {
                    _ = removed.Add(id);
                    _ = this.known.Remove(id);
                }
            }
            else if (before != this.StateOf(now))
            {
                this.known[id] = this.StateOf(now);
                if (this.Category == FeedCategory.NeedsYou && (now.ResolvedAt is not null || this.IsHidden(now)))
                {
                    _ = removed.Add(id);
                }
                else
                {
                    changed.Add(now);
                }
            }
        }

        if (changed.Count == 0 && removed.Count == 0)
        {
            return FeedRefresh.None;
        }

        Dictionary<long, FeedItem> replacements = changed.ToDictionary(static item => item.Id);
        this.Items = [.. this.Items.Where(item => !removed.Contains(item.Id)).Select(item => replacements.GetValueOrDefault(item.Id, item))];
        return new FeedRefresh(changed, removed);
    }

    /// <summary>Hides Notifications the Human just dismissed: they leave <see cref="Items"/> at once and stay out until the writer has applied the dismissal.</summary>
    /// <param name="ids">The items' ids.</param>
    /// <returns>The removal, for the list to patch the rows it holds itself.</returns>
    public FeedRefresh Hide(IReadOnlyList<long> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        HashSet<long> removed = [.. ids];
        foreach (long id in removed)
        {
            _ = this.hidden.Add(id);
            _ = this.known.Remove(id);
        }

        this.Items = [.. this.Items.Where(item => !removed.Contains(item.Id))];
        return new FeedRefresh([], removed);
    }

    /// <summary>Whether an item belongs to the list under the current filter, mirroring the store's query for items that arrive live.</summary>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when it belongs.</returns>
    public bool Matches(FeedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (this.Agent is { } name && !string.Equals(item.Actor.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (this.Team is { } team && !string.Equals(FeedSummary.TeamOf(item.Payload), team, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return this.Category switch
        {
            FeedCategory.All => true,
            FeedCategory.NeedsYou => item.Notify && item.ResolvedAt is null && !this.IsHidden(item),
            _ => item.SourceType is { } type && FeedCategories.Of(type) == this.Category,
        };
    }

    private void Load(int limit)
    {
        IReadOnlyList<FeedItem> page = store.Page(this.QueryFor(null, limit));
        this.Items = this.Visible(page);
        this.loadedCount = page.Count;
        this.lowestLoadedId = page.Count > 0 ? page[^1].Id : 0;
        this.HasMore = page.Count >= limit;
        this.Arrived = false;
        this.ListKey++;
        this.known.Clear();
        foreach (FeedItem item in this.Items)
        {
            this.Remember(item);
        }

        long newest = page.Count > 0 ? page[0].Id : 0;
        Volatile.Write(ref this.highestLoadedId, Math.Max(this.HighestLoadedId, newest));
    }

    private FeedQuery QueryFor(long? beforeId, int limit) =>
        new(beforeId, limit, this.Agent, this.Team, this.Category, hideDismissed && this.Category == FeedCategory.NeedsYou);

    private bool IsHidden(FeedItem item) => hideDismissed && (item.DismissedAt is not null || this.hidden.Contains(item.Id));

    private List<FeedItem> Visible(IReadOnlyList<FeedItem> page) =>
        this.Category == FeedCategory.NeedsYou ? [.. page.Where(item => !this.IsHidden(item))] : [.. page];

    private void Remember(FeedItem item) => this.known[item.Id] = this.StateOf(item);

    private (bool Resolved, bool Dismissed) StateOf(FeedItem item) =>
        (item.ResolvedAt is not null, hideDismissed && this.Category == FeedCategory.NeedsYou && item.DismissedAt is not null);
}
