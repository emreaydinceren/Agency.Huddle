using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Teams;

/// <summary>A register row that the next retro must check against later evidence (RT-7).</summary>
/// <param name="RowId">The row id, <c>R-001</c> and so on.</param>
/// <param name="Finding">What the retro found.</param>
/// <param name="Action">What was decided to do about it.</param>
/// <param name="EvidenceNextCheck">The evidence so far, or what to check.</param>
internal sealed record VerificationPrompt(string RowId, string Finding, string Action, string EvidenceNextCheck);

/// <summary>Picks the register rows a retro has to verify and judges the evidence offered for Verified (RT-7, Spec §Retro).</summary>
internal static class RetroVerifier
{
    private const string LogsMarker = "logs/";

    private const string LineMarker = "#L";

    private const string RetrosMarker = "_retros/";

    private static readonly char[] TokenSeparators = [' ', '\t', '\r', '\n', ',', ';'];

    private static readonly char[] TokenPunctuation = ['(', ')', '[', ']', '<', '>', '`', '\'', '"', '.', ':'];

    /// <summary>Gives the rows to check at the next retro: those marked Done or Partial. Open rows are not due; Verified and Changed rows are final.</summary>
    /// <param name="rows">The register's rows.</param>
    /// <returns>One prompt per due row, in register order.</returns>
    public static IReadOnlyList<VerificationPrompt> Due(IReadOnlyList<RegisterRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows
            .Where(static row => row.Status is RetroStatus.Done or RetroStatus.Partial)
            .Select(static row => new VerificationPrompt(row.Id, row.Finding, row.Action, row.EvidenceNextCheck))];
    }

    /// <summary>Whether evidence text links to something a reader can open: a Task id, a <c>logs/...#L12</c> line or a <c>_retros/</c> file.</summary>
    /// <param name="evidence">The evidence text.</param>
    /// <returns><see langword="true"/> when at least one word of it is such a link.</returns>
    public static bool HasEvidenceLink(string evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        foreach (string word in evidence.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            string token = word.Trim(TokenPunctuation);
            if (IsLink(token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLink(string token)
    {
        if (TaskId.TryParse(token, out _))
        {
            return true;
        }

        if (token.Contains(RetrosMarker, StringComparison.Ordinal))
        {
            return true;
        }

        int line = token.IndexOf(LineMarker, StringComparison.Ordinal);
        return token.Contains(LogsMarker, StringComparison.Ordinal)
            && line >= 0
            && line + LineMarker.Length < token.Length
            && char.IsAsciiDigit(token[line + LineMarker.Length]);
    }
}
