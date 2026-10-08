using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Hand-driven inputs for a <see cref="HireLimitGuard"/>: a <see cref="FakeTeamCatalog"/> whose Teams a
/// test sets with <see cref="SetTeam"/> and a live-process count a test sets with
/// <see cref="LiveProcesses"/>, so the hire-limit tests of the Proposal service, the propose tool and the
/// Proposal card share one arrangement.
/// </summary>
internal sealed class HireLimitHarness
{
    /// <summary>Builds a harness whose guard reads its limits from <paramref name="options"/>.</summary>
    /// <param name="options">The options carrying the per-Team and live-process limits.</param>
    public HireLimitHarness(IOptions<TeamOptions> options)
    {
        this.Guard = new HireLimitGuard(this.Catalog, options, () => this.LiveProcesses);
    }

    /// <summary>The Team catalog the guard reads member counts from.</summary>
    public FakeTeamCatalog Catalog { get; } = new();

    /// <summary>The live-process count the guard reads; a test sets it directly.</summary>
    public int LiveProcesses { get; set; }

    /// <summary>The guard under test.</summary>
    public HireLimitGuard Guard { get; }

    /// <summary>Adds a Team with <paramref name="memberCount"/> members to the catalog.</summary>
    /// <param name="name">The Team name.</param>
    /// <param name="memberCount">How many members the Team already has.</param>
    public void SetTeam(string name, int memberCount)
    {
        List<string> members = [];
        for (int i = 1; i <= memberCount; i++)
        {
            members.Add($"{name}Member{i}");
        }

        this.Catalog.Teams = [.. this.Catalog.Teams, new TeamSummary(name, [], members, false)];
    }
}
