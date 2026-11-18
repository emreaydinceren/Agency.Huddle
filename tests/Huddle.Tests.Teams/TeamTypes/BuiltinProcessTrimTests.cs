using System.Reflection;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Pins Team Playbooks spec §10: the built-in processes no longer repeat what the Member Playbook says, and each trimmed file still parses whole.</summary>
public sealed class BuiltinProcessTrimTests
{
    private const string DraftParagraphStart = "A draft that the Definition of Done reviews is a file";
    private const string DisagreementLine = "- A disagreement between agents goes to the Human with both views written on the Task.";

    /// <summary>Every built-in process file, as a resource name with forward slashes, in ordinal order, one row each.</summary>
    public static TheoryData<string> ProcessResources
    {
        get
        {
            TheoryData<string> rows = new();
            foreach (string resource in EnumerateProcessResources())
            {
                rows.Add(resource);
            }

            return rows;
        }
    }

    /// <summary>No built-in process file holds the draft-is-a-file paragraph or the identical disagreement line.</summary>
    /// <param name="resource">The resource name of one process file.</param>
    [Theory]
    [MemberData(nameof(ProcessResources))]
    public void Process_NoRepeatedPlaybookText(string resource)
    {
        string text = ReadResource(resource);

        Assert.DoesNotContain(DraftParagraphStart, text, StringComparison.Ordinal);
        Assert.DoesNotContain(DisagreementLine, text, StringComparison.Ordinal);
    }

    /// <summary>Each process file still parses with no missing outline heading, and keeps at least one Escalation bullet.</summary>
    /// <param name="resource">The resource name of one process file.</param>
    [Theory]
    [MemberData(nameof(ProcessResources))]
    public void Process_StillParsesWholeWithAnEscalationBullet(string resource)
    {
        string text = ReadResource(resource);

        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document), resource);
        Assert.NotNull(document);
        Assert.Empty(document.MissingSections);
        Assert.NotEmpty(EscalationBullets(text));
    }

    /// <summary>Each edited <c>*-basics.md</c> file's template version moved up from the one before the trim (1.0.0, or 1.1.0 for household).</summary>
    /// <param name="resource">The resource name of one process file.</param>
    [Theory]
    [MemberData(nameof(ProcessResources))]
    public void Process_EditedBasicsFile_TemplateVersionMovedUp(string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!resource.EndsWith("-basics.md", StringComparison.Ordinal) || resource.Contains("/development/", StringComparison.Ordinal))
        {
            Assert.NotEmpty(resource);
            return;
        }

        Version floor = resource.Contains("/household/", StringComparison.Ordinal) ? new Version(1, 1, 0) : new Version(1, 0, 0);
        string text = ReadResource(resource);
        string line = text.Split('\n').Select(static l => l.TrimEnd('\r')).First(static l => l.StartsWith("template:", StringComparison.Ordinal));
        string version = line[(line.IndexOf('@', StringComparison.Ordinal) + 1)..].Trim().Trim('\'');

        Assert.True(Version.Parse(version) > floor, resource + " is still at " + version);
    }

    /// <summary>Returns the bullet lines under <c>## Escalation</c>.</summary>
    /// <param name="text">The raw file text.</param>
    /// <returns>The bullets, trimmed.</returns>
    private static List<string> EscalationBullets(string text)
    {
        List<string> bullets = [];
        bool inside = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line, "## Escalation", StringComparison.Ordinal);
            }
            else if (inside && line.StartsWith("- ", StringComparison.Ordinal))
            {
                bullets.Add(line);
            }
        }

        return bullets;
    }

    /// <summary>Lists the built-in process resources.</summary>
    /// <returns>The resource names with forward slashes, ordinal order.</returns>
    private static IEnumerable<string> EnumerateProcessResources() =>
        typeof(TeamTypeCatalog).Assembly.GetManifestResourceNames()
            .Select(static n => n.Replace('\\', '/'))
            .Where(static n => n.Contains("TeamTypes/Builtin/", StringComparison.Ordinal) && n.Contains("/processes/", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)) // contains-ok: resource path filter
            .Order(StringComparer.Ordinal);

    /// <summary>Reads a built-in process resource as text.</summary>
    /// <param name="target">The resource name with forward slashes.</param>
    /// <returns>The raw file text.</returns>
    private static string ReadResource(string target)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(n => n.Replace('\\', '/') == target);
        using Stream stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException(target);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
