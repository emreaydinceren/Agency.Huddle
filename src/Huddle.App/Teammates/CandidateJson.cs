using System.Text.Json.Nodes;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Parses a <see cref="Candidate"/> (Spec §7.2) out of one JSON object from a
/// <c>propose_teammates</c> tool call. Runs Spec §8.3 order 1 only - shape: required fields,
/// forbidden fields, and JSON types. Name and Alias syntax, Title/Body blankness, and every
/// collision check are <see cref="CandidateChecker"/>'s job, extracted from
/// <c>PersonaStore.Check</c> rather than duplicated here.
/// </summary>
internal static class CandidateJson
{
    /// <summary>The fields a Candidate is allowed to carry.</summary>
    private static readonly HashSet<string> KnownFields = new(StringComparer.Ordinal) { "name", "alias", "title", "body", "teams", "consult_when" };

    /// <summary>
    /// Fields only the Human sets, on the Teammate card, once a Candidate is approved (Spec §14
    /// D-5). A Candidate that carries any of these is rejected outright, never silently stripped.
    /// </summary>
    private static readonly HashSet<string> ForbiddenFields = new(StringComparer.Ordinal) { "model", "effort", "adapter", "skills", "_builtin" };

    /// <summary>
    /// Parses one Candidate out of <paramref name="node"/>. Every problem found is appended to
    /// <paramref name="problems"/> - a missing required field, a forbidden field, an unknown
    /// field, or a field of the wrong JSON shape - rather than stopping at the first, so a
    /// proposer sees everything wrong with one Candidate in a single reply.
    /// </summary>
    /// <param name="node">The Candidate's JSON object, from the tool call's <c>candidates</c> array.</param>
    /// <param name="index">This Candidate's 1-based position, used to name it in problem text.</param>
    /// <param name="candidate">The parsed Candidate, or <see langword="null"/> if any problem was found.</param>
    /// <param name="problems">Problems found while parsing this Candidate are appended here.</param>
    /// <returns><see langword="true"/> if the Candidate parsed with no problems.</returns>
    internal static bool TryParse(JsonNode? node, int index, out Candidate? candidate, List<string> problems)
    {
        candidate = null;
        var startCount = problems.Count;

        if (node is not JsonObject fields)
        {
            problems.Add($"Candidate {index} is not a JSON object.");
            return false;
        }

        foreach (var forbiddenField in ForbiddenFields)
        {
            if (fields.ContainsKey(forbiddenField))
            {
                problems.Add($"A Candidate cannot set '{forbiddenField}'; the Human sets it on the Teammate card.");
            }
        }

        foreach (var pair in fields)
        {
            if (!KnownFields.Contains(pair.Key) && !ForbiddenFields.Contains(pair.Key))
            {
                problems.Add($"Candidate {index} has an unknown field '{pair.Key}'.");
            }
        }

        var name = ReadRequiredString(fields, "name", index, problems);
        var alias = ReadRequiredString(fields, "alias", index, problems);
        var title = ReadRequiredString(fields, "title", index, problems);
        var body = ReadRequiredString(fields, "body", index, problems);
        var teams = ReadTeams(fields, index, problems);
        var consultWhen = ReadOptionalString(fields, "consult_when");

        if (problems.Count > startCount || name is null || alias is null || title is null || body is null || teams is null)
        {
            return false;
        }

        candidate = new Candidate(name, alias, title, body, teams, consultWhen);
        return true;
    }

    /// <summary>Reads a required string field, trimmed, reporting a problem if it is absent or not a string.</summary>
    private static string? ReadRequiredString(JsonObject fields, string field, int index, List<string> problems)
    {
        if (!fields.TryGetPropertyValue(field, out var node) || node is null)
        {
            problems.Add($"Candidate {index} is missing '{field}'.");
            return null;
        }

        if (node is not JsonValue value || !value.TryGetValue(out string? text))
        {
            problems.Add($"Candidate {index}'s '{field}' must be a string.");
            return null;
        }

        return text.Trim();
    }

    /// <summary>Reads an optional string field, trimmed; a blank or absent value is <see langword="null"/>.</summary>
    private static string? ReadOptionalString(JsonObject fields, string field)
    {
        if (!fields.TryGetPropertyValue(field, out var node) || node is not JsonValue value || !value.TryGetValue(out string? text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Reads the optional 'teams' field as an array of strings. Anything else - a non-array, or
    /// an element that is not a string - is one problem naming this Candidate's index (Spec
    /// §7.2). An absent 'teams' field is not a problem; it means no Teams.
    /// </summary>
    private static List<string>? ReadTeams(JsonObject fields, int index, List<string> problems)
    {
        if (!fields.TryGetPropertyValue("teams", out var node) || node is null)
        {
            return [];
        }

        if (node is not JsonArray array)
        {
            problems.Add($"Candidate {index}'s 'teams' must be an array of strings.");
            return null;
        }

        var teams = new List<string>(array.Count);
        foreach (var element in array)
        {
            if (element is not JsonValue value || !value.TryGetValue(out string? text))
            {
                problems.Add($"Candidate {index}'s 'teams' must be an array of strings.");
                return null;
            }

            teams.Add(text.Trim());
        }

        return teams;
    }
}
