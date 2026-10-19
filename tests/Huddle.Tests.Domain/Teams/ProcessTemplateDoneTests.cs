using System.Reflection;
using System.Text;
using System.Text.Json;
using Agency.Huddle.App.Teams;
using DocumentReviewedBy = Agency.Huddle.App.Teams.DoneRule.DocumentReviewedBy;
using EvidenceFrom = Agency.Huddle.App.Teams.DoneRule.EvidenceFrom;
using Unreadable = Agency.Huddle.App.Teams.DoneRule.Unreadable;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Every built-in process template under <c>TeamTypes/Builtin</c> carries a readable <c>## Definition of Done</c>
/// (ADR-0062) whose review and evidence steps are covered by a role of the same Team type.
/// </summary>
public sealed class ProcessTemplateDoneTests
{
    private const string ResourcePrefix = "TeamTypes/Builtin/";

    /// <summary>Gets one row per built-in process template: the resource name, <c>&lt;type&gt;/processes/&lt;file&gt;.md</c>.</summary>
    public static TheoryData<string> Templates
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string name in TemplateNames())
            {
                data.Add(name);
            }

            return data;
        }
    }

    private static List<string> TemplateNames()
    {
        List<string> names = [];
        foreach (string resource in typeof(ProcessDocument).Assembly.GetManifestResourceNames())
        {
            string normalized = resource.Replace('\\', '/');
            if (normalized.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && normalized.Contains("/processes/", StringComparison.Ordinal)
                && normalized.EndsWith(".md", StringComparison.Ordinal))
            {
                names.Add(normalized[ResourcePrefix.Length..]);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string ReadResource(string relativeName)
    {
        Assembly assembly = typeof(ProcessDocument).Assembly;
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (string.Equals(resource.Replace('\\', '/'), ResourcePrefix + relativeName, StringComparison.Ordinal))
            {
                using Stream stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Resource '{resource}' could not be opened.");
                using StreamReader reader = new(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }

        throw new InvalidOperationException($"No embedded resource '{relativeName}'.");
    }

    private static string TypeId(string relativeName)
    {
        return relativeName[..relativeName.IndexOf('/', StringComparison.Ordinal)];
    }

    private static HashSet<string> CoveredSteps(string typeId)
    {
        using JsonDocument manifest = JsonDocument.Parse(ReadResource(typeId + "/team-type.json"));
        HashSet<string> steps = new(StringComparer.Ordinal);
        foreach (JsonElement role in manifest.RootElement.GetProperty("roles").EnumerateArray())
        {
            if (role.TryGetProperty("covers", out JsonElement covers))
            {
                foreach (JsonElement step in covers.EnumerateArray())
                {
                    steps.Add(step.GetString() ?? string.Empty);
                }
            }
        }

        return steps;
    }

    /// <summary>A built-in process template parses and carries one to four readable Definition of Done items.</summary>
    /// <param name="template">The template resource name below <c>TeamTypes/Builtin</c>.</param>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_HasOneToFourReadableDoneItems(string template)
    {
        Assert.True(ProcessDocument.TryParse(ReadResource(template), out ProcessDocument? document));
        Assert.NotNull(document);

        Assert.InRange(document.DefinitionOfDone.Count, 1, 4);
        Assert.DoesNotContain(document.DefinitionOfDone, static item => item.Rule is Unreadable);
    }

    /// <summary>Every evidence and review step a template names is covered by a role of its Team type.</summary>
    /// <param name="template">The template resource name below <c>TeamTypes/Builtin</c>.</param>
    [Theory]
    [MemberData(nameof(Templates))]
    public void Template_DoneStepsAreCoveredByTheTypesRoles(string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        Assert.True(ProcessDocument.TryParse(ReadResource(template), out ProcessDocument? document));
        Assert.NotNull(document);
        Assert.NotEmpty(document.DefinitionOfDone);

        HashSet<string> covered = CoveredSteps(TypeId(template));
        foreach (DoneItem item in document.DefinitionOfDone)
        {
            string? step = item.Rule switch
            {
                EvidenceFrom evidence => evidence.Step,
                DocumentReviewedBy review => review.Step,
                _ => null,
            };
            if (step is not null)
            {
                Assert.Contains(step, covered);
            }
        }
    }
}
