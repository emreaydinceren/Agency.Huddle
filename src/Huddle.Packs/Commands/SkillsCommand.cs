using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;
using System.Text.Json.Nodes;

namespace Agency.Huddle.Packs.Commands;

/// <summary><c>huddle-packs skills [&lt;text&gt;]</c>: every Skill in every catalog and Pack.</summary>
internal static class SkillsCommand
{
    private const int DescriptionWidth = 120;

    /// <summary>Prints the Skills whose name, description, category or sub-category contains the text.</summary>
    public static int Run(ParsedArguments arguments, DataLocation location, IFileReader files, TextWriter output)
    {
        Registry registry = RegistryReader.Load(files, location.SkillSourcesDir, location.Context.ComputerName);
        ResolvedRegistry resolved = RegistryResolver.Resolve(registry, files, location.Context, new ResolveOptions(IgnoreGrace: true));
        string? text = arguments.Argument;
        List<SkillEntry> skills = [.. resolved.Skills
            .Where(skill => text is null || Contains(skill.Name, text) || Contains(skill.Description, text) || Contains(skill.Category, text) || Contains(skill.SubCategory, text))
            .OrderBy(static skill => skill.Name, StringComparer.OrdinalIgnoreCase)];
        if (arguments.Json)
        {
            JsonObject root = new()
            {
                ["skills"] = new JsonArray([.. skills.Select(static skill => (JsonNode)new JsonObject
                {
                    ["name"] = skill.Name,
                    ["downloaded"] = skill.PathState == SkillPathState.Present,
                    ["sources"] = new JsonArray([.. skill.Providers.Select(static provider => (JsonNode?)JsonValue.Create(provider))]),
                    ["category"] = skill.Category,
                    ["subCategory"] = skill.SubCategory,
                    ["description"] = skill.Description,
                    ["origin"] = skill.Origin,
                })]),
            };
            output.WriteLine(FindingWriter.Serialize(root));
            return 0;
        }

        foreach (SkillEntry skill in skills)
        {
            string state = skill.PathState == SkillPathState.Present ? "downloaded" : "not-downloaded";
            string description = FindingWriter.OneLine(skill.Description);
            if (description.Length > DescriptionWidth)
            {
                description = description[..DescriptionWidth];
            }

            output.WriteLine($"{skill.Name}\t{state}\t{string.Join(',', skill.Providers)}\t{description}".TrimEnd('\t'));
        }

        return 0;
    }

    /// <summary>Ordinal containment ignoring case; false for null.</summary>
    private static bool Contains(string? value, string text)
    {
        return value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}
