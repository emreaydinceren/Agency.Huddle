using System.Text.Json;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Teams;
using Microsoft.JSInterop;

namespace Agency.Huddle.App.Components.Layout;

/// <summary>
/// Which Teams have a shortcut at the top of the Activity bar (<c>S5</c>), for one circuit. Every Team has one by default;
/// the Human removes one from its context menu, turns them all off from the same menu, and adds one back from the Team
/// page. What is remembered is the departure from that default, under <see cref="StorageKey"/> in the browser's
/// <c>localStorage</c> (as the Sidebar's width and the Teams view's open Teams are), so a Team built later gets its
/// shortcut without the Human asking.
/// </summary>
/// <remarks>
/// Scoped, so the Activity bar and the Team page of one circuit see one state and <see cref="Changed"/> keeps them in
/// step. Nothing is shown until <see cref="LoadAsync"/> has read the stored choice, so a removed shortcut never flashes
/// on a reload; JavaScript is unreachable while prerendering, so the first caller is an <c>OnAfterRenderAsync</c>.
/// </remarks>
/// <param name="js">Reaches <c>huddleStorage</c> in <c>app.js</c>.</param>
internal sealed class TeamShortcutStore(IJSRuntime js)
{
    /// <summary>The <c>localStorage</c> key the Human's choice is remembered under.</summary>
    public const string StorageKey = "shell:teamShortcuts";

    /// <summary>The Teams whose shortcut the Human removed, ignoring case as Team names do.</summary>
    private readonly HashSet<string> removed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the Human turned every shortcut off.</summary>
    private bool off;

    /// <summary>Raised after any change, the first load included.</summary>
    public event Action? Changed;

    /// <summary>Whether the stored choice has been read; until it has, no shortcut is shown and none is offered.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>The Teams that have a shortcut, in <paramref name="teams"/>'s own order; none before <see cref="LoadAsync"/>.</summary>
    /// <param name="teams">Every Team, as the catalog lists them.</param>
    /// <returns>The Teams to draw a shortcut for.</returns>
    public IReadOnlyList<TeamSummary> Visible(IReadOnlyList<TeamSummary> teams) =>
        this.IsLoaded && !this.off ? [.. teams.Where(team => !this.removed.Contains(team.Name))] : [];

    /// <summary>Whether <paramref name="team"/> has a shortcut. <see langword="true"/> before <see cref="LoadAsync"/>, so nothing offers to add one yet.</summary>
    /// <param name="team">The Team's name.</param>
    /// <returns>Whether the Activity bar shows a shortcut to the Team.</returns>
    public bool Has(string team) => !this.IsLoaded || (!this.off && !this.removed.Contains(team));

    /// <summary>Reads the stored choice once per circuit; later calls do nothing.</summary>
    public async Task LoadAsync()
    {
        if (this.IsLoaded)
        {
            return;
        }

        try
        {
            string? stored = await BrowserStorage.TryGetAsync(js, StorageKey).ConfigureAwait(false);
            if (stored is not null && JsonSerializer.Deserialize<Choice>(stored) is { } choice)
            {
                this.off = choice.Off;
                foreach (string? team in choice.Removed ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(team))
                    {
                        this.removed.Add(team);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Stale or hand-edited text: every Team keeps its default shortcut.
        }

        this.IsLoaded = true;
        this.Changed?.Invoke();
    }

    /// <summary>
    /// Gives <paramref name="team"/> a shortcut. When every shortcut was turned off, this one comes back alone: the
    /// other Teams are recorded as removed, so turning them off is not undone behind the Human's back.
    /// </summary>
    /// <param name="team">The Team to add.</param>
    /// <param name="teams">Every Team, as the catalog lists them.</param>
    public Task AddAsync(string team, IReadOnlyList<TeamSummary> teams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentNullException.ThrowIfNull(teams);
        if (this.off)
        {
            this.off = false;
            this.removed.UnionWith(teams.Select(static summary => summary.Name));
        }

        this.removed.Remove(team);
        return this.SaveAsync();
    }

    /// <summary>Takes <paramref name="team"/>'s shortcut off the Activity bar.</summary>
    /// <param name="team">The Team to remove.</param>
    public Task RemoveAsync(string team)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        this.removed.Add(team);
        return this.SaveAsync();
    }

    /// <summary>Takes every shortcut off the Activity bar, including those of Teams built later.</summary>
    public Task TurnOffAsync()
    {
        this.off = true;
        this.removed.Clear();
        return this.SaveAsync();
    }

    /// <summary>Raises <see cref="Changed"/> and remembers the choice; a blocked storage keeps it for this circuit only.</summary>
    private async Task SaveAsync()
    {
        this.Changed?.Invoke();
        string json = JsonSerializer.Serialize(new Choice(this.off, [.. this.removed.Order(StringComparer.OrdinalIgnoreCase)]));

        // A blocked storage or a gone circuit keeps the choice for this circuit only.
        _ = await BrowserStorage.TrySetAsync(js, StorageKey, json).ConfigureAwait(false);
    }

    /// <summary>The stored shape: whether every shortcut is off, and the Teams removed one by one.</summary>
    /// <param name="Off">Whether every shortcut is off.</param>
    /// <param name="Removed">The Teams removed one by one.</param>
    private sealed record Choice(bool Off, string?[]? Removed);
}
