namespace Agency.Huddle.App.Acp.Tools;

using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Contracts;

/// <summary>
/// Lists every Agent registered in the Team Directory and every available Persona, noting which
/// Agents are online and each entry's job description composed from its Persona's frontmatter
/// (see <see cref="PersonaFrontmatter"/>), when one is present.
/// </summary>
internal sealed class ListAgentsTool(ITeamDirectory teamDirectory, IAgentGateway agentGateway, PersonaStore personaStore) : IAppTool
{
    public string Name => "list_agents";

    public string Description =>
        "Lists every agent known to the Team application: each registered Agent, noting whether it is currently " +
        "online, and each available Persona that could be brought online. Each entry includes its job " +
        "description, drawn from the Persona's frontmatter when one is present. Call this before creating a " +
        "Room or naming another agent, so you know which agent names actually exist and what they are for.";

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray(),
    };

    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var users = await teamDirectory.GetUsersAsync(cancellationToken);
        var agents = users.Where(u => u.Kind == UserKind.Agent).ToList();
        var personaNames = personaStore.ListNames();

        var builder = new StringBuilder();

        builder.Append("Agents:");
        if (agents.Count == 0)
        {
            builder.Append(" none");
        }
        else
        {
            foreach (var agent in agents)
            {
                var status = agentGateway.IsOnline(agent.Id) ? "online" : "offline";
                builder.Append('\n').Append("- ").Append(agent.Name).Append(" (").Append(status).Append(')');
                this.AppendJobDescription(builder, agent.Name);
            }
        }

        builder.Append("\n\nPersonas:");
        if (personaNames.Count == 0)
        {
            builder.Append(" none");
        }
        else
        {
            foreach (var name in personaNames)
            {
                builder.Append('\n').Append("- ").Append(name);
                this.AppendJobDescription(builder, name);
            }
        }

        return builder.ToString();
    }

    /// <summary>Appends a Persona's composed job description, indented under its bullet, if it has one.</summary>
    private void AppendJobDescription(StringBuilder builder, string personaName)
    {
        var persona = personaStore.Get(personaName);
        if (persona is null)
        {
            return;
        }

        var jobDescription = PersonaFrontmatter.ComposeJobDescription(persona.Text);
        if (jobDescription.Length == 0)
        {
            return;
        }

        foreach (var line in jobDescription.Split('\n'))
        {
            builder.Append('\n').Append("  ").Append(line);
        }
    }
}