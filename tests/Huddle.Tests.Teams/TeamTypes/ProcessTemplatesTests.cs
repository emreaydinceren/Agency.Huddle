using System.Reflection;
using System.Text.RegularExpressions;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the four shipped Development process templates (PR-1, PR-6): each has the PR-6 outline with the Onboarding section, a template id at version 1.0.0, known gates, and role ids that exist in the manifest.</summary>
public sealed partial class ProcessTemplatesTests
{
    private const string TypeFolder = "TeamTypes/Builtin/development/";

    private static readonly string[] OutlineHeadings =
    [
        "Identity",
        "When it applies",
        "Stages",
        "Who does what",
        "If a role is not hired",
        "Gates",
        "Definition of Done",
        "Cadence",
        "Escalation",
        "Onboarding",
    ];

    private static readonly string[] KnownGates =
    [
        "qa-evidence-before-done",
        "checks-before-merge",
        "spec-approved-before-build",
        "failing-test-before-fix",
    ];

    /// <summary>A process file split into the lines between its frontmatter fences and the lines of its body.</summary>
    /// <param name="Frontmatter">The lines between the two <c>---</c> fences, without the fences.</param>
    /// <param name="Body">The lines after the closing fence.</param>
    private sealed record ProcessFile(IReadOnlyList<string> Frontmatter, IReadOnlyList<string> Body);

    [GeneratedRegex("`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickSpan();

    /// <summary>Reads an embedded process file and splits it into frontmatter and body, so the tests see the files that ship.</summary>
    /// <param name="processId">The process id, which is also the file name without <c>.md</c>.</param>
    /// <returns>The parsed frontmatter and body lines.</returns>
    private static ProcessFile LoadProcess(string processId)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string target = TypeFolder + "processes/" + processId + ".md";
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == target);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        string[] lines = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("---", lines[0]);
        int closing = Array.IndexOf(lines, "---", 1);
        Assert.True(closing > 0);
        return new ProcessFile(lines[1..closing], lines[(closing + 1)..]);
    }

    /// <summary>Returns the value of one frontmatter key with its quotes removed, and fails the test when the key is absent.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="key">The frontmatter key, such as <c>template</c>.</param>
    /// <returns>The scalar value.</returns>
    private static string Scalar(ProcessFile file, string key)
    {
        string prefix = key + ":";
        string? line = file.Frontmatter.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
        Assert.NotNull(line);
        return line[prefix.Length..].Trim().Trim('\'');
    }

    /// <summary>Returns the items of a frontmatter flow list such as <c>gates: ['a', 'b']</c>.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="key">The frontmatter key holding the list.</param>
    /// <returns>The list items in file order, without quotes.</returns>
    private static string[] ListValue(ProcessFile file, string key)
    {
        string raw = Scalar(file, key);
        return raw.Trim('[', ']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static item => item.Trim('\''))
            .ToArray();
    }

    /// <summary>Returns the level-two headings of a process body, in document order.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <returns>The heading texts without the <c>## </c> marker.</returns>
    private static string[] Headings(ProcessFile file)
    {
        return file.Body
            .Where(static l => l.StartsWith("## ", StringComparison.Ordinal))
            .Select(static l => l[3..])
            .ToArray();
    }

    /// <summary>Returns the body lines under one level-two heading, up to the next heading.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="heading">The heading text, without the <c>## </c> marker.</param>
    /// <returns>The lines of that section.</returns>
    private static List<string> Section(ProcessFile file, string heading)
    {
        List<string> lines = [];
        bool inside = false;
        foreach (string line in file.Body)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line[3..], heading, StringComparison.Ordinal);
                continue;
            }

            if (inside)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>Extracts the text of every backtick span in the given lines.</summary>
    /// <param name="lines">The lines to scan.</param>
    /// <returns>Each span's text, in order.</returns>
    private static IEnumerable<string> BacktickSpans(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            foreach (Match match in BacktickSpan().Matches(line))
            {
                yield return match.Groups[1].Value;
            }
        }
    }

    /// <summary>Reads the embedded Development manifest, so role ids are checked against the real roles.</summary>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest LoadManifest()
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(static n => n.Replace('\\', '/') == TypeFolder + "team-type.json");
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        TeamTypeManifest? manifest = System.Text.Json.JsonSerializer.Deserialize<TeamTypeManifest>(reader.ReadToEnd(), TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Each process has the PR-6 outline in order, followed by the Onboarding section.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_Outline_IsPR6HeadingsInOrderThenOnboarding(string processId)
    {
        ProcessFile file = LoadProcess(processId);

        Assert.Equal(OutlineHeadings, Headings(file));
    }

    /// <summary>Each process has a title and a template id that is its file name at version 1.0.0.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_Frontmatter_TemplateIsFileNameAtVersion(string processId)
    {
        ProcessFile file = LoadProcess(processId);

        Assert.NotEmpty(Scalar(file, "title"));
        Assert.Equal(processId + "@1.0.0", Scalar(file, "template"));
    }

    /// <summary>Each process declares exactly the gates the plan gives it, and every gate is one of the four known ids.</summary>
    /// <param name="processId">The process file to check.</param>
    /// <param name="expectedGates">The expected gate ids, in file order.</param>
    [Theory]
    [InlineData("lightweight-flow", new[] { "qa-evidence-before-done", "checks-before-merge" })]
    [InlineData("spec-driven", new[] { "qa-evidence-before-done", "checks-before-merge", "spec-approved-before-build" })]
    [InlineData("sprints", new[] { "qa-evidence-before-done", "checks-before-merge" })]
    [InlineData("maintenance", new[] { "qa-evidence-before-done", "checks-before-merge", "failing-test-before-fix" })]
    public void Process_Gates_AreExpectedKnownIds(string processId, string[] expectedGates)
    {
        ProcessFile file = LoadProcess(processId);

        string[] gates = ListValue(file, "gates");
        Assert.All(gates, gate => Assert.Contains(gate, KnownGates));
        Assert.Equal(expectedGates, gates);
    }

    /// <summary>Every role id named in a process's Who does what section exists in the Development manifest.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_WhoDoesWhat_NamesOnlyManifestRoles(string processId)
    {
        ProcessFile file = LoadProcess(processId);
        string[] roles = BacktickSpans(Section(file, "Who does what")).ToArray();
        string[] manifestRoles = LoadManifest().Roles.Select(static r => r.Id).ToArray();

        Assert.NotEmpty(roles);
        Assert.All(roles, role => Assert.Contains(role, manifestRoles));
    }
}
