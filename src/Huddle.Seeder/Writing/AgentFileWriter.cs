using System.Text;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>Renders Agent definition files and Skill files.</summary>
internal static class AgentFileWriter
{
    /// <summary>Renders <c>Agents/&lt;Name&gt;/&lt;Name&gt;.md</c>.</summary>
    /// <param name="agent">The Agent.</param>
    /// <returns>The file text.</returns>
    internal static string RenderAgent(SeedAgent agent)
    {
        StringBuilder text = new();
        text.Append("---\n");
        text.Append("name: ").Append(Quote(agent.Name)).Append('\n');
        text.Append("title: ").Append(Quote(agent.Title)).Append('\n');
        text.Append("alias: ").Append(Quote(agent.Alias)).Append('\n');
        AppendList(text, "teams", agent.Teams);
        AppendList(text, "skills", agent.Skills);
        text.Append("specialty: ").Append(Quote(agent.Specialty)).Append('\n');
        text.Append("---\n");
        text.Append(agent.Personality.ReplaceLineEndings("\n").Trim()).Append('\n');
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
