using System.Text;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>Renders Teammate definition files and Skill files.</summary>
internal static class TeammateFileWriter
{
    /// <summary>Renders <c>Teammates/&lt;Name&gt;/&lt;Name&gt;.md</c>.</summary>
    /// <param name="teammate">The Teammate.</param>
    /// <returns>The file text.</returns>
    internal static string RenderTeammate(SeedTeammate teammate)
    {
        StringBuilder text = new();
        text.Append("---\n");
        text.Append("name: ").Append(Quote(teammate.Name)).Append('\n');
        text.Append("title: ").Append(Quote(teammate.Title)).Append('\n');
        text.Append("alias: ").Append(Quote(teammate.Alias)).Append('\n');
        AppendList(text, "teams", teammate.Teams);
        AppendList(text, "skills", teammate.Skills);
        text.Append("specialty: ").Append(Quote(teammate.Specialty)).Append('\n');
        text.Append("---\n");
        text.Append(teammate.Personality.ReplaceLineEndings("\n").Trim()).Append('\n');
        return text.ToString();
    }

    /// <summary>Renders <c>Skills/&lt;name&gt;/SKILL.md</c>.</summary>
    /// <param name="skill">The Skill.</param>
    /// <returns>The file text.</returns>
    internal static string RenderSkill(SeedSkill skill) =>
        $"---\nname: {skill.Name}\ndescription: {skill.Description}\n---\n{skill.Body.ReplaceLineEndings("\n").Trim()}\n";

    private static void AppendList(StringBuilder text, string key, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            text.Append(key).Append(": [").Append(string.Join(", ", values.Select(Quote))).Append("]\n");
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
