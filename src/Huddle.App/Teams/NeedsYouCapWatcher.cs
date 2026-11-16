using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Re-evaluates the scopes that count a Persona's spend each time it grows, so the question that a cap raises opens as the cap is reached
/// and not only when something next reads a level (ADR-0088). It holds no timer or token: it is a subscription for the life of the host.
/// </summary>
/// <param name="spend">Raises an event when a Persona's persisted spend grows.</param>
/// <param name="ledger">Receives each USD increase, charged to the scopes the Persona is in at that moment.</param>
/// <param name="ids">Gives each Team the id its ledger row is keyed by.</param>
/// <param name="levels">Evaluating a level is what opens the question, so the watcher asks for the levels of the scopes the Persona counts in.</param>
/// <param name="teams">Names the Teams a Persona belongs to, and their Projects.</param>
/// <param name="roomSpend">Says which Project a Room belongs to, because a Project counts whoever ran the Turn, a member of its Team or not.</param>
internal sealed class NeedsYouCapWatcher(PersonaSpend spend, NeedsYouLevels levels, ITeamCatalog teams, NeedsYouSpendStore ledger, ScopeIdStore ids, RoomSpendStore roomSpend) : IHostedService
{
    /// <summary>Starts listening.</summary>
    /// <param name="cancellationToken">Unused; subscribing is immediate.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        spend.SpendAdded += this.OnSpendAdded;
        spend.SpendChanged += this.OnSpendChanged;
        return Task.CompletedTask;
    }

    /// <summary>Stops listening.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        spend.SpendChanged -= this.OnSpendChanged;
        spend.SpendAdded -= this.OnSpendAdded;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Charges a USD increase to the global scope and to every Team the Persona belongs to now, before <see cref="OnSpendChanged"/> reads the
    /// levels. Another currency is not counted.
    /// </summary>
    /// <param name="increase">The Persona, currency and amount the report added.</param>
    internal void OnSpendAdded(PersonaSpend.SpendIncrease increase)
    {
        if (!string.Equals(increase.Currency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<string> scopes = [NeedsYouCaps.GlobalKey];
        scopes.AddRange(teams.Teams
            .Where(t => t.Members.Contains(increase.PersonaName, StringComparer.OrdinalIgnoreCase))
            .Select(t => NeedsYouCaps.TeamKey(ids.TeamId(t.Name))));
        ledger.Add(scopes, increase.Amount);

        // A Project counts the Room's spend whoever ran the Turn, so its level is evaluated even when the spender is no member of its Team.
        if (increase.RoomId is not null && roomSpend.ProjectOf(increase.RoomId) is { } scope)
        {
            _ = levels.For(scope.Team, scope.Project);
        }
    }

    /// <summary>Evaluates the global scope, and each Team and Project the Persona counts in.</summary>
    /// <param name="personaName">The Persona that spent.</param>
    internal void OnSpendChanged(string personaName)
    {
        _ = levels.For(string.Empty, null);
        foreach (TeamSummary team in teams.Teams.Where(t => t.Members.Contains(personaName, StringComparer.OrdinalIgnoreCase)))
        {
            _ = levels.For(team.Name, null);
            foreach (string project in team.Projects)
            {
                _ = levels.For(team.Name, project);
            }
        }
    }
}
