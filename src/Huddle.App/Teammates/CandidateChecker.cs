using System.Text.RegularExpressions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Tells an Agent, for free and with no side effects, every reason a Candidate would not become a
/// Teammate (Spec §6.8). Runs Spec §8.3 order 2 through 6, in that order, collecting every
/// problem rather than stopping at the first - <see cref="CandidateJson"/> already ran order 1
/// (shape) before a Candidate ever reaches here.
/// </summary>
/// <remarks>
/// The joint collision check (orders 4 and 5) is one call to <see cref="PersonaStore.Check"/>,
/// which cannot tell a Candidate colliding with a sibling Candidate apart from one colliding with
/// the library - both produce the same rejection text, quoting an absolute filesystem path.
/// <see cref="RewriteCollisionMessage"/> is what tells them apart afterwards: a quoted path that
/// really exists is rewritten relative to the data directory; a quoted path that does not exist
/// is <see cref="PersonaStore.Check"/>'s own synthetic stand-in for a sibling Candidate (Spec
/// §6.8's "Each text gets a synthetic path"), rewritten to name that Candidate instead. Neither a
/// synthetic path nor an absolute one is ever model-facing text on its own.
/// </remarks>
internal sealed partial class CandidateChecker(PersonaStore personas, ITeamDirectory directory, IAgentGateway gateway)
{
    private const string FrontmatterDelimiter = "---";

    /// <summary>
    /// Checks every Candidate in <paramref name="candidates"/> and returns every problem found,
    /// across all of them, together. Never throws for an invalid Candidate: a
    /// <see cref="ChatException"/> raised by <see cref="PersonaStore"/> along the way is caught
    /// and turned into a problem string instead.
    /// </summary>
    /// <param name="candidates">The Candidates to check, in the order they were proposed.</param>
    /// <param name="ct">Cancels the Team Directory lookups this performs.</param>
    /// <returns>Every problem found, and whether there were none.</returns>
    internal async Task<CandidateCheck> CheckAsync(IReadOnlyList<Candidate> candidates, CancellationToken ct)
    {
        var problems = new List<string>();
        var sound = new List<(Candidate Candidate, string Text)>();

        // Spec §8.3 orders 2 and 3: per-Candidate structural checks. A Candidate that fails one of
        // these is excluded from the joint collision check below - composing and checking text for
        // a Candidate whose own Name, Alias, Title or Body is already broken would either fail to
        // parse back (leaking PersonaStore.Check's internal "__check-N" placeholder path) or check
        // nothing meaningful, so its own problem here is reported instead and nothing more.
        foreach (var candidate in candidates)
        {
            if (AddStructuralProblems(candidate, problems))
            {
                sound.Add((candidate, ComposeText(candidate)));
            }
        }

        // Spec §8.3 orders 4 and 5, in one joint call (Spec §6.8's "joint: PersonaStore.Check(texts)").
        if (sound.Count > 0)
        {
            var results = personas.Check(sound.Select(pair => pair.Text).ToList());
            foreach (var (_, problem) in results)
            {
                if (problem is not null)
                {
                    problems.Add(this.RewriteCollisionMessage(problem));
                }
            }
        }

        // Spec §8.3 order 6 (Reserved Names) and the Internal flow's own "files" step, per Candidate.
        var human = await directory.GetHumanAsync(ct);
        foreach (var candidate in candidates)
        {
            await this.AddReservedNameProblemsAsync(candidate, human, problems, ct);
            this.AddFileExistsProblem(candidate, problems);
        }

        // Spec §6.8's own note: "If one collision produces the same message for several
        // Candidates, dedupe exact duplicate problem strings" - Distinct keeps first-occurrence
        // order, so this changes nothing about which problem is reported first.
        var distinct = problems.Distinct(StringComparer.Ordinal).ToList();
        return new CandidateCheck(distinct.Count == 0, distinct);
    }

    /// <summary>
    /// Adds every Spec §8.3 order 2/3 problem <paramref name="candidate"/> has - an invalid Name
    /// or Alias, a blank Title or Body, a Body starting with <c>---</c> (Spec §6.8's
    /// implementation notes: it would read as a second frontmatter block), or a Team containing
    /// <c>,</c> or <c>;</c> (Spec §12 F-24: it would split into two Teams on read) - to
    /// <paramref name="problems"/>.
    /// </summary>
    /// <returns><see langword="true"/> if none of those problems were found.</returns>
    private static bool AddStructuralProblems(Candidate candidate, List<string> problems)
    {
        var sound = true;

        if (!NameRules.IsValidAgentName(candidate.Name))
        {
            problems.Add($"'{candidate.Name}' is not a valid Name: letters, digits, '-' and '_', with single spaces between words.");
            sound = false;
        }

        if (!NameRules.IsValidAgentName(candidate.Alias))
        {
            problems.Add($"'{candidate.Alias}' is not a valid Alias: letters, digits, '-' and '_', with single spaces between words.");
            sound = false;
        }

        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            problems.Add($"{candidate.Name} has a blank Title.");
            sound = false;
        }

        if (string.IsNullOrWhiteSpace(candidate.Body))
        {
            problems.Add($"{candidate.Name} has a blank Body.");
            sound = false;
        }
        else if (candidate.Body.StartsWith(FrontmatterDelimiter, StringComparison.Ordinal))
        {
            problems.Add($"{candidate.Name}'s Body must not start with '---'; it would read as a second frontmatter block.");
            sound = false;
        }

        var badTeam = candidate.Teams.FirstOrDefault(team => team.Contains(',', StringComparison.Ordinal) || team.Contains(';', StringComparison.Ordinal));
        if (badTeam is not null)
        {
            problems.Add($"{candidate.Name}'s Team '{badTeam}' must not contain ',' or ';'.");
            sound = false;
        }

        return sound;
    }

    /// <summary>
    /// Adds a problem if <paramref name="candidate"/>'s Name is reserved (Spec §8.3 order 6, §12
    /// F-9): the Human's own Name, or a connected Agent with no Persona backing it - a demo Agent
    /// or a raw pipe client - whose <c>hello</c> a same-named new Persona would otherwise attach to.
    /// </summary>
    private async Task AddReservedNameProblemsAsync(Candidate candidate, User human, List<string> problems, CancellationToken ct)
    {
        if (string.Equals(candidate.Name, human.Name, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"'{candidate.Name}' is the Human's Name.");
            return;
        }

        var user = await directory.FindUserByNameAsync(candidate.Name, ct);
        if (user is { Kind: UserKind.Agent } && gateway.IsOnline(user.Id) && personas.ResolveByNameOrAlias(candidate.Name) is null)
        {
            problems.Add($"'{candidate.Name}' is a connected Agent that is not a Teammate.");
        }
    }

    /// <summary>
    /// Adds a problem if a file already sits at the path <see cref="PersonaStore.Add"/> would
    /// write <paramref name="candidate"/>'s Name to (Spec §6.8's "files" step) - checked directly
    /// against the filesystem, since a file that never became a valid Persona is invisible to
    /// <see cref="PersonaStore.Entries"/> and <see cref="PersonaStore.Check"/>'s collision rules
    /// alike. <see cref="PersonaStore.EnsureNoFileExistsFor"/> throws the same
    /// <see cref="ChatException"/> <see cref="PersonaStore.Add"/> itself would throw for this
    /// reason; per this class's constraints, that is caught and turned into a problem string.
    /// </summary>
    private void AddFileExistsProblem(Candidate candidate, List<string> problems)
    {
        try
        {
            personas.EnsureNoFileExistsFor(candidate.Name);
        }
        catch (ChatException ex)
        {
            problems.Add(ex.Message);
        }
    }

    /// <summary>Composes the text <paramref name="candidate"/> would be written as, adding <c>consult_when</c> when set (Spec §7.2).</summary>
    private static string ComposeText(Candidate candidate)
    {
        var identity = new PersonaIdentity(candidate.Name, candidate.Title, candidate.Alias, candidate.Teams);
        var text = PersonaFrontmatter.Compose(identity, candidate.Body);

        return string.IsNullOrWhiteSpace(candidate.ConsultWhen)
            ? text
            : PersonaFrontmatter.WriteScalarField(text, "consult_when", candidate.ConsultWhen);
    }

    /// <summary>
    /// Rewrites every quoted <c>.md</c> path in one <see cref="PersonaStore.Check"/> rejection
    /// message so nothing filesystem-shaped reaches the model: a path that really exists under the
    /// data directory becomes a relative <c>Teammates/…</c> path with forward slashes; a path that
    /// does not exist is one of <see cref="PersonaStore.Check"/>'s own synthetic stand-ins for a
    /// sibling Candidate, rewritten to name that Candidate instead. A message naming several paths
    /// (Spec §8.3's "joined multi-sentence messages") is rewritten sentence by sentence, in place.
    /// </summary>
    private string RewriteCollisionMessage(string message)
    {
        return QuotedMarkdownPathRegex().Replace(message, match =>
        {
            var path = match.Groups[1].Value;
            if (File.Exists(path))
            {
                // GetRelativePath is relative to DataDir, so the result already carries the
                // Teammates/<Name>/<Name>.md path a Human can follow from the DataDir root -
                // no prefix needs adding back here, unlike the old flat Teams/ layout.
                var relative = Path.GetRelativePath(personas.Paths.DataDir, path).Replace('\\', '/');
                return $"'{relative}'";
            }

            return $"Candidate '{CandidateNameFromSyntheticPath(path)}'";
        });
    }

    /// <summary>
    /// Recovers a Candidate's Name from one of <see cref="PersonaStore.Check"/>'s synthetic
    /// paths - <c>{Name}.md</c> for the first Candidate proposing that Name, <c>{Name}~{n}.md</c>
    /// for a later one proposing the same Name again.
    /// </summary>
    private static string CandidateNameFromSyntheticPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var tildeIndex = name.LastIndexOf('~');

        return tildeIndex > 0 && name[(tildeIndex + 1)..].All(char.IsAsciiDigit) ? name[..tildeIndex] : name;
    }

    /// <summary>Matches one single-quoted, <c>.md</c>-suffixed path inside a <see cref="PersonaStore.Check"/> rejection message.</summary>
    [GeneratedRegex(@"'([^']*\.md)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedMarkdownPathRegex();
}

/// <summary>The result of <see cref="CandidateChecker.CheckAsync"/>: every problem found across every Candidate checked, and whether there were none.</summary>
/// <param name="IsValid"><see langword="true"/> when <paramref name="Problems"/> is empty.</param>
/// <param name="Problems">Every problem found, in the order <see cref="CandidateChecker.CheckAsync"/> found them, with exact duplicates removed.</param>
public sealed record CandidateCheck(bool IsValid, IReadOnlyList<string> Problems);
