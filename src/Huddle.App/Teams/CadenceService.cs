using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Wakes a Team's manager for a digest and its Retro agent for a retro, on a schedule or after a count of Done Tasks,
/// as the Team's <c>Process.md</c> declares (OR-3, RT-2, Spec §Cadence). Every minute it looks at each Team once.
/// The last firing lives in <c>team.db</c>, so a restart repeats nothing (NF-8): the first time a Team is seen only
/// sets a baseline. A digest is time-based only, because the state holds no Done count for it.
/// </summary>
/// <param name="catalog">Lists the Teams, their managers and their members.</param>
/// <param name="folders">Locates each Team's folder, where <c>Process.md</c> lives.</param>
/// <param name="types">Resolves a Team's type and its Retro role.</param>
/// <param name="personas">Reads each member's role.</param>
/// <param name="tasks">Counts Done Tasks.</param>
/// <param name="state">Remembers what fired when.</param>
/// <param name="prompts">Supplies the digest and retro texts.</param>
/// <param name="poster">Delivers a text to a Teammate's Room, which wakes it.</param>
/// <param name="logger">Reports a Team that could not be checked.</param>
/// <param name="timeProvider">The clock, and the timer's clock; defaults to the system clock.</param>
internal sealed class CadenceService(
    ITeamCatalog catalog,
    ITeamFolders folders,
    TeamTypeCatalog types,
    PersonaStore personas,
    TaskStore tasks,
    CadenceStateStore state,
    IPromptSource prompts,
    IOnboardingPoster poster,
    ILogger<CadenceService> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    /// <summary>The prompt key of the digest text.</summary>
    internal const string DigestPrompt = "prompt.digest";

    /// <summary>The prompt key of the retro text.</summary>
    internal const string RetroPrompt = "prompt.retro";

    /// <summary>Put before the retro text when it goes to the manager because no Retro agent is hired.</summary>
    internal const string NoRetroAgentNote = "No Retro agent is hired on this Team, so please run this retro yourself.\n\n";

    private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Runs one pass over every Team; the timer calls it each minute.</summary>
    /// <param name="ct">Stops the pass.</param>
    internal async Task TickAsync(CancellationToken ct)
    {
        foreach (TeamSummary team in catalog.Teams)
        {
            ct.ThrowIfCancellationRequested();
            if (!team.HasFolder || team.Manager is null)
            {
                continue;
            }

            try
            {
                await this.TickTeamAsync(team, team.Manager, ct).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                // The prompt behind this cadence is not in the catalog (yet); skip the Team and retry next minute.
                logger.LogWarning("Cadence prompt missing for Team {Team}.", team.Name);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Period, this.clock);
        do
        {
            try
            {
                await this.TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The supervisor loop must not die: log and try again on the next minute.
                logger.LogError(ex, "Cadence pass failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private static bool Elapsed(DateTimeOffset now, DateTimeOffset since, int days) =>
        (now - since) >= TimeSpan.FromDays(days);

    private static string Since(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task TickTeamAsync(TeamSummary team, string manager, CancellationToken ct)
    {
        ProcessDocument? process = this.ReadProcess(team.Name);
        if (process is null || (process.Digest is null && process.Retro is null))
        {
            return;
        }

        DateTimeOffset now = this.clock.GetUtcNow();
        int done = this.CountDone(team.Name);
        CadenceState current = state.Get(team.Name) ?? new CadenceState(team.Name, null, null, null);
        bool digestBaselined = process.Digest is not null && current.LastDigestUtc is null;
        bool retroBaselined = process.Retro is not null && (current.LastRetroUtc is null || current.DoneAtLastRetro is null);
        if (digestBaselined || retroBaselined)
        {
            current = current with
            {
                LastDigestUtc = digestBaselined ? now : current.LastDigestUtc,
                LastRetroUtc = retroBaselined ? now : current.LastRetroUtc,
                DoneAtLastRetro = retroBaselined ? done : current.DoneAtLastRetro,
            };
            state.Set(current);
        }

        if (!digestBaselined && process.Digest is { Unit: CadenceUnit.Days } digest
            && current.LastDigestUtc is { } lastDigest && Elapsed(now, lastDigest, digest.Every))
        {
            string text = prompts.Render(DigestPrompt, Values(team.Name, lastDigest));
            current = await this.FireAsync(current with { LastDigestUtc = now }, current, manager, text, ct).ConfigureAwait(false);
        }

        if (!retroBaselined && process.Retro is { } retro
            && current.LastRetroUtc is { } lastRetro && current.DoneAtLastRetro is { } doneAtLast
            && RetroDue(retro, now, lastRetro, done - doneAtLast))
        {
            string text = prompts.Render(RetroPrompt, Values(team.Name, lastRetro));
            (string recipient, string message) = this.RetroRecipient(team, manager, text);
            _ = await this.FireAsync(current with { LastRetroUtc = now, DoneAtLastRetro = done }, current, recipient, message, ct).ConfigureAwait(false);
        }
    }

    private static bool RetroDue(RetroCadence retro, DateTimeOffset now, DateTimeOffset last, int newDone) =>
        retro.Unit == CadenceUnit.DoneTasks ? newDone >= retro.Every : Elapsed(now, last, retro.Every);

    private static Dictionary<string, string> Values(string team, DateTimeOffset since) =>
        new(StringComparer.Ordinal)
        {
            ["{{team}}"] = team,
            ["{{since}}"] = Since(since),
        };

    /// <summary>Records the firing first, then posts; a refused or failed post puts the previous state back so the next tick retries.</summary>
    private async Task<CadenceState> FireAsync(CadenceState fired, CadenceState previous, string recipient, string text, CancellationToken ct)
    {
        state.Set(fired);
        bool posted = false;
        try
        {
            posted = await poster.PostAsync(recipient, text, ct).ConfigureAwait(false);
        }
        finally
        {
            if (!posted)
            {
                state.Set(previous);
            }
        }

        return posted ? fired : previous;
    }

    private (string Recipient, string Message) RetroRecipient(TeamSummary team, string manager, string text)
    {
        string? retroRole = team.TeamType is { } typeRef ? types.Find(typeRef.Id)?.RetroRole : null;
        if (retroRole is not null && team.TeamType is { } type)
        {
            foreach (string member in team.Members)
            {
                if (personas.Get(member) is { } persona
                    && PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
                    && identity.Role is { } role
                    && string.Equals(role.TypeId, type.Id, StringComparison.Ordinal)
                    && string.Equals(role.RoleId, retroRole, StringComparison.Ordinal))
                {
                    return (member, text);
                }
            }
        }

        return (manager, NoRetroAgentNote + text);
    }

    private int CountDone(string team) =>
        tasks.All.Count(item => item.Status == TaskState.Done && string.Equals(item.Location.Team, team, StringComparison.OrdinalIgnoreCase));

    private ProcessDocument? ReadProcess(string team)
    {
        LibraryResult<LibraryPath> folder = folders.FindTeam(team);
        if (folder.Value is null)
        {
            return null;
        }

        string path = Path.Combine(folder.Value.FullPath, TeamPaths.ProcessFileName);
        try
        {
            return File.Exists(path) && ProcessDocument.TryParse(File.ReadAllText(path), out ProcessDocument? document) ? document : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
