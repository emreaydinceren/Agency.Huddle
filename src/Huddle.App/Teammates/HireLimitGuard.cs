using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Teammates;

/// <summary>A hire the limits refuse: which limit, how much of it is used and how many more would fit.</summary>
/// <param name="Kind">The limit that refused.</param>
/// <param name="Team">The Team that is full for <see cref="HireLimitKind.PerTeam"/>; <see langword="null"/> for the live-process limit.</param>
/// <param name="Used">Members of that Team, or live processes, before the hire.</param>
/// <param name="Limit">The limit's value.</param>
internal sealed record HireRefusal(HireLimitKind Kind, string? Team, int Used, int Limit)
{
    /// <summary>How many more hires fit before the limit.</summary>
    public int Fits => Math.Max(0, this.Limit - this.Used);
}

/// <summary>The limit with the least room left for a set of Candidates.</summary>
/// <param name="Kind">Which limit it is.</param>
/// <param name="Team">The Team for <see cref="HireLimitKind.PerTeam"/>; <see langword="null"/> for the live-process limit.</param>
/// <param name="Used">Members of that Team, or live processes, now.</param>
/// <param name="Limit">The limit's value.</param>
internal sealed record HireHeadroom(HireLimitKind Kind, string? Team, int Used, int Limit);

/// <summary>
/// Applies <see cref="HireLimits"/> to a set of Candidates using the live Team catalog and the live
/// process count, so the three places that cap hiring agree.
/// </summary>
internal sealed class HireLimitGuard
{
    private readonly ITeamCatalog catalog;
    private readonly IOptions<TeamOptions> options;
    private readonly Func<int> liveProcesses;

    /// <summary>Creates the guard.</summary>
    /// <param name="catalog">Where each Team's member count comes from.</param>
    /// <param name="options">Supplies the two limits.</param>
    /// <param name="liveProcesses">Reads the number of running agent processes.</param>
    public HireLimitGuard(ITeamCatalog catalog, IOptions<TeamOptions> options, Func<int> liveProcesses)
    {
        this.catalog = catalog;
        this.options = options;
        this.liveProcesses = liveProcesses;
    }

    /// <summary>Checks the Candidates against the per-Team limit of each Team they name, then the live-process limit.</summary>
    /// <param name="candidates">The Candidates being hired.</param>
    /// <returns>The first refusal, or <see langword="null"/> when the hire fits.</returns>
    public HireRefusal? Check(IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        AcpOptions acp = this.options.Value.Acp;

        foreach (KeyValuePair<string, int> team in HireLimitGuard.CountPerTeam(candidates))
        {
            int members = this.MembersOf(team.Key);
            HireLimitResult perTeam = HireLimits.Check(members, team.Value, 0, acp.MaxTeammatesPerTeam, 0);
            if (!perTeam.Allowed)
            {
                return new HireRefusal(HireLimitKind.PerTeam, team.Key, members, perTeam.Limit);
            }
        }

        int live = this.liveProcesses();
        HireLimitResult liveResult = HireLimits.Check(0, candidates.Count, live, 0, acp.MaxLiveProcesses);
        return liveResult.Allowed ? null : new HireRefusal(HireLimitKind.LiveProcesses, null, live, liveResult.Limit);
    }

    /// <summary>Finds the limit with the least room left, for the Proposal card's headroom line; a tie goes to the per-Team limit.</summary>
    /// <param name="candidates">The Candidates being hired.</param>
    /// <returns>The tightest limit, or <see langword="null"/> when both limits are off.</returns>
    public HireHeadroom? Tightest(IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        AcpOptions acp = this.options.Value.Acp;
        HireHeadroom? tightest = null;

        if (acp.MaxTeammatesPerTeam > 0)
        {
            foreach (string team in HireLimitGuard.CountPerTeam(candidates).Keys)
            {
                int members = this.MembersOf(team);
                if (tightest is null || (acp.MaxTeammatesPerTeam - members) < (tightest.Limit - tightest.Used))
                {
                    tightest = new HireHeadroom(HireLimitKind.PerTeam, team, members, acp.MaxTeammatesPerTeam);
                }
            }
        }

        if (acp.MaxLiveProcesses > 0)
        {
            int live = this.liveProcesses();
            if (tightest is null || (acp.MaxLiveProcesses - live) < (tightest.Limit - tightest.Used))
            {
                tightest = new HireHeadroom(HireLimitKind.LiveProcesses, null, live, acp.MaxLiveProcesses);
            }
        }

        return tightest;
    }

    /// <summary>Counts how many Candidates name each Team, ignoring case and blank names; the key keeps the first spelling seen.</summary>
    /// <param name="candidates">The Candidates being hired.</param>
    private static Dictionary<string, int> CountPerTeam(IReadOnlyList<Candidate> candidates)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        foreach (Candidate candidate in candidates)
        {
            foreach (string team in candidate.Teams.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                counts[team] = counts.GetValueOrDefault(team) + 1;
            }
        }

        return counts;
    }

    /// <summary>The current member count of <paramref name="team"/>, or zero when the catalog does not know it.</summary>
    /// <param name="team">The Team name.</param>
    private int MembersOf(string team) => this.catalog.Find(team)?.Members.Count ?? 0;
}
