using System.Globalization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Counts what a scope that runs inside the Team has spent against its cap, and asks the Human once the cap is reached (ADR-0087). Spend
/// is recorded per Agent, so the global scope counts every Agent's persisted USD spend and a Team or Project scope counts its Team's
/// members. The count starts when the Human sets the level or cap, or when the scope is first evaluated with a cap, and it starts again
/// when the cap in the file differs from the one counted for.
/// </summary>
/// <remarks>
/// Only the Human answers: <see cref="Grant"/> and <see cref="Decline"/> are for the page that shows <see cref="Pending"/>, and no Agent
/// tool calls them. A Yes adds exactly one more cap to the allowance. Nothing here stops an Agent's Turn.
/// </remarks>
/// <param name="store">Holds each scope's counting row.</param>
/// <param name="spend">Reads what the Personas spent.</param>
/// <param name="teams">Names a Team's members.</param>
/// <param name="feed">Receives the Notification and its resolution.</param>
/// <param name="logger">Logs a subscriber that threw; optional.</param>
internal sealed class NeedsYouCaps(
    NeedsYouCapStore store,
    PersonaSpendStore spend,
    ITeamCatalog teams,
    IFeedPublisher feed,
    ILogger<NeedsYouCaps>? logger = null)
{
    /// <summary>The key of the global scope.</summary>
    public const string GlobalKey = "global";

    private const string Usd = "USD";
    private const string TeamPrefix = "team:";
    private const string ProjectPrefix = "project:";

    private readonly Lock gate = new();

    /// <summary>Raised after a question opened, was answered or was cancelled, so a page can repaint. A subscriber that throws is logged and does not stop the others.</summary>
    public event Action? Changed;

    /// <summary>The questions waiting for the Human, in scope-key order.</summary>
    public IReadOnlyList<CapAsk> Pending
    {
        get
        {
            lock (this.gate)
            {
                return [.. store.Asked().Select(this.ToAsk)];
            }
        }
    }

    /// <summary>The key of a Team's scope.</summary>
    /// <param name="team">The Team's name.</param>
    public static string TeamKey(string team) => TeamPrefix + team;

    /// <summary>The key of a Project's scope.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="project">The Project's name.</param>
    public static string ProjectKey(string team, string project) => ProjectPrefix + team + "/" + project;

    /// <summary>The key of the scope that supplied a resolved level.</summary>
    /// <param name="resolved">The level.</param>
    /// <param name="team">The Team the level was asked for, which names the Project's Team.</param>
    public static string KeyOf(EffectiveLevel resolved, string team)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        return resolved.Scope switch
        {
            NeedsYouScope.Project => ProjectKey(team, resolved.ScopeName ?? string.Empty),
            NeedsYouScope.Team => TeamKey(resolved.ScopeName ?? team),
            _ => GlobalKey,
        };
    }

    /// <summary>
    /// Whether the scope that supplied <paramref name="resolved"/> has spent its allowance, starting the count when it has none. The first
    /// time it finds the allowance spent it opens the question and posts the Notification, once per allowance.
    /// </summary>
    /// <param name="resolved">A level of Run inside the Team with a spend cap.</param>
    /// <param name="team">The Team the level was asked for.</param>
    /// <returns><see langword="true"/> when the scope must fall back to Ask me.</returns>
    public bool IsReached(EffectiveLevel resolved, string team)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        if (resolved.SpendCap is not { } cap)
        {
            return false;
        }

        string key = KeyOf(resolved, team);
        bool opened = false;
        bool reached;
        lock (this.gate)
        {
            decimal total = this.Total(key);
            CapRow row = store.Get(key) is { } existing && existing.Cap == cap ? existing : this.Start(key, cap, total);
            decimal spent = total - row.Baseline;
            reached = row.Declined || spent >= row.Allowance;
            if (reached && !row.Declined && !row.Asked)
            {
                row = row with { Asked = true };
                store.Save(row);
                feed.Post(Notification(row, spent));
                opened = true;
            }
        }

        if (opened)
        {
            this.RaiseChanged();
        }

        return reached;
    }

    /// <summary>Starts the count again for a scope after the Human set its level or cap, or ends it when the scope no longer runs inside the Team with a cap.</summary>
    /// <param name="key">The scope key.</param>
    /// <param name="cap">The cap in USD, or <see langword="null"/> for none.</param>
    public void Restart(string key, decimal? cap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        bool cancelled;
        lock (this.gate)
        {
            CapRow? existing = store.Get(key);
            cancelled = existing is { Asked: true };
            if (cancelled)
            {
                feed.Resolve(key, FeedSourceType.SpendCapReached);
            }

            if (cap is { } usable)
            {
                store.Save(new CapRow(key, usable, this.Total(key), usable, false, false));
            }
            else
            {
                store.Delete(key);
            }
        }

        if (cancelled)
        {
            this.RaiseChanged();
        }
    }

    /// <summary>The Human said Yes: the scope may spend exactly one more cap before it is asked again.</summary>
    /// <param name="key">The scope key of a <see cref="CapAsk"/>.</param>
    /// <returns><see langword="false"/> when no question was open for the scope.</returns>
    public bool Grant(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return this.Answer(key, static row => row with { Allowance = row.Allowance + row.Cap, Asked = false, Declined = false });
    }

    /// <summary>The Human said No: the scope stays at Ask me until the Human changes its level or cap.</summary>
    /// <param name="key">The scope key of a <see cref="CapAsk"/>.</param>
    /// <returns><see langword="false"/> when no question was open for the scope.</returns>
    public bool Decline(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return this.Answer(key, static row => row with { Asked = false, Declined = true });
    }

    /// <summary>The scope's first-line name for a message: <c>Huddle</c>, the Team, or <c>Team / Project</c>.</summary>
    private static string NameOf(string key)
    {
        if (key.StartsWith(ProjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key[ProjectPrefix.Length..].Replace("/", " / ", StringComparison.Ordinal);
        }

        return key.StartsWith(TeamPrefix, StringComparison.OrdinalIgnoreCase) ? key[TeamPrefix.Length..] : "Huddle";
    }

    private static (string? Team, string? Project) Split(string key)
    {
        if (key.StartsWith(ProjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rest = key[ProjectPrefix.Length..];
            int slash = rest.IndexOf('/', StringComparison.Ordinal);
            return slash < 0 ? (rest, null) : (rest[..slash], rest[(slash + 1)..]);
        }

        return key.StartsWith(TeamPrefix, StringComparison.OrdinalIgnoreCase) ? (key[TeamPrefix.Length..], null) : (null, null);
    }

    private static string Money(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private bool Answer(string key, Func<CapRow, CapRow> change)
    {
        lock (this.gate)
        {
            if (store.Get(key) is not { Asked: true } row)
            {
                return false;
            }

            store.Save(change(row));
            feed.Resolve(row.Scope, FeedSourceType.SpendCapReached);
        }

        this.RaiseChanged();
        return true;
    }

    private CapRow Start(string key, decimal cap, decimal total)
    {
        if (store.Get(key) is { Asked: true })
        {
            feed.Resolve(key, FeedSourceType.SpendCapReached);
        }

        CapRow row = new(key, cap, total, cap, false, false);
        store.Save(row);
        return row;
    }

    private CapAsk ToAsk(CapRow row)
    {
        (string? team, string? project) = Split(row.Scope);
        return new CapAsk(row.Scope, NameOf(row.Scope), team, project, row.Cap, this.Total(row.Scope) - row.Baseline);
    }

    private static FeedMessage Notification(CapRow row, decimal spent)
    {
        string limit = row.Allowance == row.Cap ? "its cap" : "its allowance";
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"{NameOf(row.Scope)} has spent {Money(spent)} USD running inside the Team, {limit}. Let it spend another {Money(row.Cap)} USD?");
        return new FeedMessage(
            FeedSourceType.SpendCapReached,
            row.Scope,
            FeedActor.Huddle,
            true,
            new SpendCapReachedPayload(row.Scope, Split(row.Scope).Team, row.Cap, message));
    }

    /// <summary>What the counted Personas have spent in USD in all: every Persona for the global scope, the Team's members otherwise.</summary>
    private decimal Total(string key)
    {
        IEnumerable<StoredSpend> amounts;
        if (Split(key).Team is { } team)
        {
            amounts = teams.Find(team) is { } summary ? spend.ForPersonas(summary.Members) : [];
        }
        else
        {
            amounts = spend.All().Select(static row => new StoredSpend(row.Currency, row.Amount));
        }

        return amounts.Where(static amount => string.Equals(amount.Currency, Usd, StringComparison.OrdinalIgnoreCase)).Sum(static amount => amount.Amount);
    }

    private void RaiseChanged() =>
        EventRaise.Isolated(this.Changed, ex => logger?.LogWarning(ex, "A subscriber to the spend cap question threw."));
}
