using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Guarantees the Chief of Staff exists (Spec §6.12). Registered as a hosted service that runs
/// once at startup, before <see cref="Acp.PersonaSupervisor"/> starts any Teammate: if no loaded
/// Persona already carries the <see cref="BuiltinTeammate.ChiefOfStaffMarker"/>, this writes the
/// embedded default (<see cref="BuiltinTeammate.DefaultText"/>) through <see cref="PersonaStore.Add"/>,
/// under the first free (Name, Alias) pair Spec §8.6's search finds when the shipped defaults are
/// already taken.
/// </summary>
/// <remarks>
/// Runs regardless of <c>Team:Acp:Enabled</c> - writing a file is free; only starting a session
/// spends. Never reverts an edit: only an absent marker triggers a write, and the check runs
/// once, at startup, never on <c>PersonaRemoved</c> (Spec §6.12 Constraints). A failed write -
/// no free (Name, Alias) pair within <see cref="MaxFreeNameAttempts"/> attempts, or
/// <see cref="PersonaStore.Add"/> itself throwing - logs an Error and lets startup continue
/// (Spec §8.7): nothing this hosted service does ever escapes <see cref="StartAsync"/>.
/// </remarks>
/// <param name="personas">The Persona library to check and seed.</param>
/// <param name="logger">Logs a failed parse, a failed free-name search, or a failed write, as an Error, without stopping the host.</param>
internal sealed class BuiltinTeammateSeeder(PersonaStore personas, ILogger<BuiltinTeammateSeeder> logger) : IHostedService
{
    /// <summary>The default Chief of Staff Name the free-name search starts from (Spec §8.6).</summary>
    private const string DefaultName = "Chief of Staff";

    /// <summary>The default Chief of Staff Alias the free-name search starts from (Spec §8.6).</summary>
    private const string DefaultAlias = "cos";

    /// <summary>
    /// How many <c>(Name, Alias)</c> pairs the free-name search tries before giving up. Spec §8.6
    /// does not itself bound the search, but an installation with a hundred Personas already named
    /// "Chief of Staff N" is not a free-name search away from a usable one - past this point,
    /// giving up and logging is more honest than searching forever.
    /// </summary>
    private const int MaxFreeNameAttempts = 100;

    /// <summary>
    /// Writes the default Chief of Staff, under the first free (Name, Alias) pair, when no loaded
    /// Persona carries the marker.
    /// </summary>
    /// <param name="cancellationToken">Unused; the write is synchronous local file I/O.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var alreadySeeded = personas.Entries.Any(
            entry => string.Equals(entry.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal));
        if (alreadySeeded)
        {
            return Task.CompletedTask;
        }

        if (!PersonaFrontmatter.TryReadIdentity(BuiltinTeammate.DefaultText, out PersonaIdentity? defaultIdentity, out var parseError))
        {
            logger.LogError("Could not seed the Chief of Staff: the embedded default failed to parse ({Error}).", parseError);
            return Task.CompletedTask;
        }

        if (!this.TryFindFreeNameAndAlias(out var name, out var alias))
        {
            logger.LogError("Could not seed the Chief of Staff: no free Name/Alias pair was found within {MaxAttempts} attempts.", MaxFreeNameAttempts);
            return Task.CompletedTask;
        }

        var identity = defaultIdentity with { Name = name, Alias = alias };
        var (_, body) = PersonaFrontmatter.Parse(BuiltinTeammate.DefaultText);

        try
        {
            personas.Add(identity, body, model: null, effort: null);
        }
        catch (ChatException ex)
        {
            logger.LogError(ex, "Could not seed the Chief of Staff '{Name}': {Reason}.", identity.Name, ex.Message);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Could not seed the Chief of Staff '{Name}': {Reason}.", identity.Name, ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>Does nothing; the seeder has no state to release.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Finds the first free <c>(Name, Alias)</c> pair from Spec §8.6's search:
    /// <c>Chief of Staff</c> / <c>cos</c>, then <c>Chief of Staff 2</c> / <c>cos2</c>, and so on. A
    /// pair is free when neither half equals any loaded Persona's Name or Alias - compared the way
    /// <see cref="PersonaIndex"/> itself compares them, case-insensitively - and no
    /// <c>{Name}.md</c> file already sits on disk, checked through <see cref="PersonaStore.TeamsDirectory"/>
    /// rather than recomputing the Teams path.
    /// </summary>
    /// <param name="name">The first free Name, when this returns <see langword="true"/>; otherwise <see cref="string.Empty"/>.</param>
    /// <param name="alias">The matching free Alias, when this returns <see langword="true"/>; otherwise <see cref="string.Empty"/>.</param>
    /// <returns><see langword="true"/> if a free pair was found within <see cref="MaxFreeNameAttempts"/> attempts.</returns>
    private bool TryFindFreeNameAndAlias(out string name, out string alias)
    {
        for (var n = 1; n <= MaxFreeNameAttempts; n++)
        {
            var candidateName = n == 1 ? DefaultName : $"{DefaultName} {n}";
            var candidateAlias = n == 1 ? DefaultAlias : $"{DefaultAlias}{n}";

            var taken = personas.Entries.Any(entry =>
                string.Equals(entry.Name, candidateName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Alias, candidateName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Name, candidateAlias, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Alias, candidateAlias, StringComparison.OrdinalIgnoreCase));

            if (!taken && !File.Exists(Path.Combine(personas.TeamsDirectory, $"{candidateName}.md")))
            {
                name = candidateName;
                alias = candidateAlias;
                return true;
            }
        }

        name = string.Empty;
        alias = string.Empty;
        return false;
    }
}
