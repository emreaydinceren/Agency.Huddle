using Agency.Huddle.SkillPacks.Checking;
using System.Text.Json.Nodes;

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
