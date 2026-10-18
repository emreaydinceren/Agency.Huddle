using System.Text.Json.Nodes;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Packs.Commands;

/// <summary><c>huddle-packs roles</c>: every role id a Pack may name.</summary>
internal static class RolesCommand
{
    /// <summary>Prints the roles, sorted ordinally ignoring case.</summary>
    public static int Run(ParsedArguments arguments, CheckContext context, TextWriter output)
    {
        List<RoleInfo> roles = [.. context.Roles.OrderBy(static role => role.Id, StringComparer.OrdinalIgnoreCase)];
        if (arguments.Json)
        {
            JsonObject root = new()
            {
                ["roles"] = new JsonArray([.. roles.Select(static role => (JsonNode)new JsonObject { ["id"] = role.Id, ["title"] = role.Title, ["description"] = role.Description })]),
            };
            output.WriteLine(FindingWriter.Serialize(root));
            return 0;
        }

        foreach (RoleInfo role in roles)
        {
            output.WriteLine($"{role.Id}\t{role.Title}");
        }

        return 0;
    }
}

/// <summary><c>huddle-packs installed</c>: every Skill in <c>Skills/</c>.</summary>
internal static class InstalledCommand
{
    /// <summary>Prints the installed Skills, sorted ordinally ignoring case.</summary>
    public static int Run(ParsedArguments arguments, CheckContext context, TextWriter output)
    {
        List<InstalledSkill> skills = [.. context.InstalledSkills.OrderBy(static skill => skill.Name, StringComparer.OrdinalIgnoreCase)];
        if (arguments.Json)
        {
            JsonObject root = new()
            {
                ["installed"] = new JsonArray([.. skills.Select(static skill => (JsonNode)new JsonObject
                {
                    ["name"] = skill.Name,
                    ["origin"] = skill.Origin,
                    ["sha256"] = skill.Sha256,
                    ["builtIn"] = skill.BuiltIn,
                })]),
            };
            output.WriteLine(FindingWriter.Serialize(root));
            return 0;
        }

        foreach (InstalledSkill skill in skills)
        {
            string origin = skill.BuiltIn ? "built-in" : skill.Origin ?? string.Empty;
            output.WriteLine($"{skill.Name}\t{origin}\t{skill.Sha256 ?? string.Empty}".TrimEnd('\t'));
        }

        return 0;
    }
}

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

/// <summary><c>huddle-packs schema pack|catalog|source</c>: the embedded JSON Schema.</summary>
internal static class SchemaCommand
{
    /// <summary>Prints the schema text, unchanged, then a newline.</summary>
    public static int Run(string kind, TextWriter output, TextWriter error)
    {
        string? text = SchemaFiles.Read(kind);
        if (text is null)
        {
            error.WriteLine($"Unknown schema {kind}.");
            return 2;
        }

        output.WriteLine(text.TrimEnd('\r', '\n'));
        return 0;
    }
}
