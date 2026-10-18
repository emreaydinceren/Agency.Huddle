using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.Packs;

/// <summary>One line of <c>validate</c> output before the columns are padded.</summary>
/// <param name="Severity"><c>ERROR</c>, <c>WARN</c> or <c>OK</c>.</param>
/// <param name="Code">The code; empty for <c>OK</c>.</param>
/// <param name="File">The file as shown.</param>
/// <param name="Message">The message on one line.</param>
internal sealed record OutputLine(string Severity, string Code, string File, string Message);

/// <summary>Writes <c>validate</c>'s lines and JSON (§6.4, "Output lines").</summary>
internal static class FindingWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>A file as shown: relative to the Skill Sources folder with <c>/</c> when under it, else absolute.</summary>
    public static string Display(string file, string skillSourcesDir)
    {
        if (Checks.IsUnder(file, skillSourcesDir) && !Checks.IsUnder(skillSourcesDir, file))
        {
            return Path.GetRelativePath(skillSourcesDir, file).Replace('\\', '/');
        }

        return Checks.IsUnder(skillSourcesDir, file) && Checks.IsUnder(file, skillSourcesDir) ? "." : file;
    }

    /// <summary>The line for a finding.</summary>
    public static OutputLine Line(Finding finding, string skillSourcesDir)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new OutputLine(finding.Severity == Severity.Error ? "ERROR" : "WARN", finding.Code, Display(finding.File, skillSourcesDir), OneLine(finding.Message));
    }

    /// <summary>Writes the lines in fixed columns separated by two spaces, with no trailing spaces.</summary>
    public static void WriteLines(TextWriter output, IReadOnlyList<OutputLine> lines)
    {
        int fileWidth = lines.Count == 0 ? 0 : lines.Max(static line => line.File.Length);
        foreach (OutputLine line in lines)
        {
            string text = $"{line.Severity,-5}  {line.Code,-4}  {line.File.PadRight(fileWidth)}  {line.Message}";
            output.WriteLine(text.TrimEnd());
        }
    }

    /// <summary>The summary line, always last.</summary>
    public static string Summary(CheckSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return string.Create(CultureInfo.InvariantCulture, $"PACKS-VALIDATE Rows={summary.Rows} Packs={summary.Packs} Skills={summary.Skills} Errors={summary.Errors} Warnings={summary.Warnings}");
    }

    /// <summary>The <c>--json</c> document: the summary and the findings (errors only with <paramref name="quiet"/>).</summary>
    public static string Json(CheckResult result, string skillSourcesDir, bool quiet)
    {
        ArgumentNullException.ThrowIfNull(result);
        JsonObject root = new()
        {
            ["summary"] = new JsonObject
            {
                ["rows"] = result.Summary.Rows,
                ["packs"] = result.Summary.Packs,
                ["skills"] = result.Summary.Skills,
                ["errors"] = result.Summary.Errors,
                ["warnings"] = result.Summary.Warnings,
            },
            ["findings"] = new JsonArray([.. result.Findings
                .Where(finding => !quiet || finding.Severity == Severity.Error)
                .Select(finding => (JsonNode)new JsonObject
                {
                    ["severity"] = finding.Severity == Severity.Error ? "error" : "warning",
                    ["code"] = finding.Code,
                    ["file"] = Display(finding.File, skillSourcesDir),
                    ["message"] = OneLine(finding.Message),
                })]),
        };
        return root.ToJsonString(JsonOptions);
    }

    /// <summary>Serialises a node for the list commands with the shared options.</summary>
    public static string Serialize(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.ToJsonString(JsonOptions);
    }

    /// <summary>The message with newlines replaced by a space.</summary>
    public static string OneLine(string message)
    {
        return message.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
    }
}
