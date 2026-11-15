using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Re-evaluates the scopes that count a Persona's spend each time it grows, so the question that a cap raises opens as the cap is reached
/// and not only when something next reads a level (ADR-0088). It holds no timer or token: it is a subscription for the life of the host.
/// </summary>
/// <param name="spend">Raises an event when a Persona's persisted spend grows.</param>
/// <param name="levels">Evaluating a level is what opens the question, so the watcher asks for the levels of the scopes the Persona counts in.</param>
/// <param name="teams">Names the Teams a Persona belongs to, and their Projects.</param>
internal sealed class NeedsYouCapWatcher(PersonaSpend spend, NeedsYouLevels levels, ITeamCatalog teams) : IHostedService
{
    /// <summary>Starts listening.</summary>
    /// <param name="cancellationToken">Unused; subscribing is immediate.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        spend.SpendChanged += this.OnSpendChanged;
        return Task.CompletedTask;
    }

    /// <summary>Stops listening.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        spend.SpendChanged -= this.OnSpendChanged;
        return Task.CompletedTask;
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
