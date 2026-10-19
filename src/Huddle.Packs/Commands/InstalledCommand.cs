using Agency.Huddle.SkillPacks.Checking;
using System.Text.Json.Nodes;

namespace Agency.Huddle.Packs.Commands;

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
