using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Shows a retro agent's proposed changes to the Human and applies the ones the Human accepts (RT-5, Spec §Retro). Huddle, not the
/// agent, makes the edit: the replacement is applied only when <see cref="RetroChange.Find"/> occurs exactly once in the file as it is
/// at Accept time, an Agent file goes through <see cref="PersonaStore.Update"/> so Persona rules still apply, and the change is recorded
/// as one <see cref="RetroStatus.Done"/> row with a single <see cref="RetroFileStore.AppendRows"/> call.
/// </summary>
/// <param name="proposals">Holds the pending sets.</param>
/// <param name="teams">Gives each Team's members.</param>
/// <param name="folders">Finds the Team folder.</param>
/// <param name="personas">Reads and updates Agent files.</param>
/// <param name="retro">Appends the register row.</param>
/// <param name="directory">Reads the room's members and the Human.</param>
/// <param name="chat">Posts the outcome in the room.</param>
/// <param name="clock">Dates the register row.</param>
/// <param name="logger">Logs a register row that could not be written.</param>
internal sealed class RetroProposalService(
    RetroProposalStore proposals,
    ITeamCatalog teams,
    ITeamFolders folders,
    PersonaStore personas,
    RetroFileStore retro,
    ITeamDirectory directory,
    ChatService chat,
    TimeProvider clock,
    ILogger<RetroProposalService> logger)
{
    private const string NoMatchText = "The text to replace is not in the file exactly once.";

    private const string NoReason = "no reason given.";

    private readonly Lock gate = new();

    /// <summary>The sets waiting in a room: those whose proposer is a member of it.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="ct">Cancels the lookup.</param>
    /// <returns>The pending sets.</returns>
    public async Task<IReadOnlyList<RetroProposalSet>> PendingAsync(string roomId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(roomId, ct).ConfigureAwait(false);
        List<RetroProposalSet> pending = [];
        foreach (TeamSummary team in teams.Teams)
        {
            pending.AddRange(proposals.Pending(team.Name)
                .Where(set => members.Any(member => member.Kind == UserKind.Agent && string.Equals(member.Name, set.Proposer, StringComparison.OrdinalIgnoreCase))));
        }

        return pending;
    }

    /// <summary>The unified diff of a change against the file as it is now.</summary>
    /// <param name="set">The set the change belongs to.</param>
    /// <param name="change">The change.</param>
    /// <returns>The diff, or a note when the text to replace is not in the file exactly once.</returns>
    public string Preview(RetroProposalSet set, RetroChange change)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(change);

        if (!this.TryRead(set.Team, change.TargetFile, out string text, out _))
        {
            return NoMatchText;
        }

        string find = RetroDiff.FitLineEndings(text, change.Find);
        string replace = RetroDiff.FitLineEndings(text, change.Replace);
        return RetroDiff.CountMatches(text, find) == 1 ? RetroDiff.Unified(change.TargetFile, text, find, replace) : NoMatchText;
    }

    /// <summary>Applies one change, once, and records it in the register.</summary>
    /// <param name="roomId">The room the card is in.</param>
    /// <param name="setId">The set's id.</param>
    /// <param name="changeId">The change's id.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns>What happened.</returns>
    public async Task<RetroChangeOutcome> AcceptAsync(string roomId, string setId, string changeId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeId);

        if (!await this.IsPendingAsync(roomId, setId, changeId, ct).ConfigureAwait(false))
        {
            return Gone();
        }

        RetroProposalSet? set;
        RetroChangeOutcome outcome;
        lock (this.gate)
        {
            set = proposals.Get(setId);
            RetroChange? change = set?.Changes.FirstOrDefault(each => string.Equals(each.Id, changeId, StringComparison.Ordinal));
            if (set is null || change is null)
            {
                return Gone();
            }

            outcome = this.Apply(set, change);
        }

        if (outcome.Kind == RetroChangeOutcomeKind.Applied)
        {
            string text = outcome.Text + await HumanPost.MentionAsync(directory, set.Proposer, ct).ConfigureAwait(false);
            await this.PostAsync(roomId, text, ct).ConfigureAwait(false);
            return outcome with { Text = text };
        }

        return outcome;
    }

    /// <summary>Drops one change without writing anything to the Team's files or register.</summary>
    /// <param name="roomId">The room the card is in.</param>
    /// <param name="setId">The set's id.</param>
    /// <param name="changeId">The change's id.</param>
    /// <param name="reason">Why the Human rejected it; blank says no reason was given.</param>
    /// <param name="ct">Cancels the post.</param>
    /// <returns>What happened.</returns>
    public async Task<RetroChangeOutcome> RejectAsync(string roomId, string setId, string changeId, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(setId);
        ArgumentException.ThrowIfNullOrWhiteSpace(changeId);
        ArgumentNullException.ThrowIfNull(reason);

        if (!await this.IsPendingAsync(roomId, setId, changeId, ct).ConfigureAwait(false))
        {
            return Gone();
        }

        RetroProposalSet? set = proposals.Get(setId);
        RetroChange? change = set?.Changes.FirstOrDefault(each => string.Equals(each.Id, changeId, StringComparison.Ordinal));
        if (set is null || change is null || !proposals.RemoveChange(setId, changeId))
        {
            return Gone();
        }

        string reasonText = string.IsNullOrWhiteSpace(reason) ? NoReason : reason.Trim();
        string text = $"Rejected '{change.Title}': {reasonText}{await HumanPost.MentionAsync(directory, set.Proposer, ct).ConfigureAwait(false)}";
        await this.PostAsync(roomId, text, ct).ConfigureAwait(false);
        return new RetroChangeOutcome(RetroChangeOutcomeKind.Rejected, text);
    }

    private static RetroChangeOutcome Gone() => new(RetroChangeOutcomeKind.Gone, string.Empty);

    private static RetroChangeOutcome Refused(string text) => new(RetroChangeOutcomeKind.Refused, text);

    private async Task<bool> IsPendingAsync(string roomId, string setId, string changeId, CancellationToken ct)
    {
        IReadOnlyList<RetroProposalSet> pending = await this.PendingAsync(roomId, ct).ConfigureAwait(false);
        return pending.Any(set => string.Equals(set.Id, setId, StringComparison.Ordinal)
            && set.Changes.Any(change => string.Equals(change.Id, changeId, StringComparison.Ordinal)));
    }

    /// <summary>Makes the edit under <see cref="gate"/>: re-reads the file, needs exactly one match, writes, records the row and removes the change.</summary>
    private RetroChangeOutcome Apply(RetroProposalSet set, RetroChange change)
    {
        string file = change.TargetFile;
        string failed = $"The change could not be applied to {file}.";
        if (!this.TryRead(set.Team, file, out string text, out string? path))
        {
            return Refused(failed);
        }

        string find = RetroDiff.FitLineEndings(text, change.Find);
        int matches = RetroDiff.CountMatches(text, find);
        if (matches == 0)
        {
            return Refused($"The text to replace is not in {file}; nothing changed.");
        }

        if (matches > 1)
        {
            return Refused($"The text to replace occurs more than once in {file}; nothing changed.");
        }

        string edited = text.Replace(find, RetroDiff.FitLineEndings(text, change.Replace), StringComparison.Ordinal);
        TeamFile? teamFile = null;
        if (string.Equals(file, TeamPaths.TeamFileName, StringComparison.Ordinal) && !TeamFile.TryParse(edited, out teamFile))
        {
            return Refused($"The edit would leave {file} unreadable; nothing changed.");
        }

        if (path is null
            && this.MemberPersona(set.Team, file) is { } member
            && PersonaFrontmatter.TryReadIdentity(edited, out PersonaIdentity? identity, out _)
            && !string.Equals(identity.Name, member.Name, StringComparison.Ordinal))
        {
            return Refused($"A retro change cannot rename an Agent; {file} was not changed.");
        }

        if (!this.TryWrite(set.Team, file, path, edited, teamFile))
        {
            return Refused(failed);
        }

        this.Record(set, change);
        _ = proposals.RemoveChange(set.Id, change.Id);
        return new RetroChangeOutcome(RetroChangeOutcomeKind.Applied, $"Applied '{change.Title}' to {file}.");
    }

    private void Record(RetroProposalSet set, RetroChange change)
    {
        try
        {
            string id = RetroRegister.NextId(retro.ReadRegister(set.Team));
            string evidence = $"Accepted {clock.GetUtcNow().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}; verify at the next retro.";
            retro.AppendRows(set.Team, [new RegisterRow(id, change.Title, "Edited " + change.TargetFile, set.Proposer, RetroStatus.Done, evidence)]);
        }
        catch (IOException ex)
        {
            logger.LogWarning("The register row for retro change {ChangeId} of {Team} could not be written: {Problem}", change.Id, set.Team, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("The register row for retro change {ChangeId} of {Team} could not be written: {Problem}", change.Id, set.Team, ex.Message);
        }
    }

    /// <summary>Reads the target file's current text. <c>path</c> is the file for the two Team files and <see langword="null"/> for an Agent file.</summary>
    private bool TryRead(string team, string file, out string text, out string? path)
    {
        text = string.Empty;
        path = null;
        if (string.Equals(file, TeamPaths.AgentsMdFileName, StringComparison.Ordinal) || string.Equals(file, TeamPaths.TeamFileName, StringComparison.Ordinal))
        {
            LibraryResult<LibraryPath> folder = folders.FindTeam(team);
            if (folder.Value is not { } found)
            {
                return false;
            }

            path = Path.Combine(found.FullPath, file);
            try
            {
                text = File.ReadAllText(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        if (this.MemberPersona(team, file) is { } persona)
        {
            text = persona.Text;
            return true;
        }

        return false;
    }

    private bool TryWrite(string team, string file, string? path, string edited, TeamFile? teamFile)
    {
        if (teamFile is not null)
        {
            try
            {
                return folders.UpdateTeamFile(team, _ => teamFile);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        if (path is not null)
        {
            try
            {
                AtomicFile.WriteText(path, edited);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        if (this.MemberPersona(team, file) is not { } persona)
        {
            return false;
        }

        try
        {
            _ = personas.Update(persona.Name, edited, persona.Model, persona.Effort, persona.WorkMode);
            return true;
        }
        catch (ChatException)
        {
            return false;
        }
    }

    /// <summary>The Persona a <c>Name.md</c> target names, when the Name is a member of the Team.</summary>
    private Persona? MemberPersona(string team, string file)
    {
        const string Extension = ".md";
        if (!file.EndsWith(Extension, StringComparison.Ordinal) || file.Length == Extension.Length)
        {
            return null;
        }

        string name = file[..^Extension.Length];
        TeamSummary? summary = teams.Find(team);
        return summary is not null && summary.Members.Contains(name, StringComparer.OrdinalIgnoreCase) ? personas.Get(name) : null;
    }

    /// <summary>Posts <paramref name="text"/> in the room as the Human; a refused post is logged, never thrown.</summary>
    /// <param name="roomId">The room to post in.</param>
    /// <param name="text">The text.</param>
    /// <param name="ct">Cancels the post.</param>
    private async Task PostAsync(string roomId, string text, CancellationToken ct)
    {
        string? problem = await HumanPost.TryPostAsync(chat, directory, roomId, text, ct).ConfigureAwait(false);
        if (problem is not null)
        {
            logger.LogWarning("Could not post the retro proposal outcome in room {RoomId}: {Problem}", roomId, problem);
        }
    }
}
