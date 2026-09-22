namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;

/// <summary>Returns a held Skill's body, or one of its supporting files, to the calling Agent (Spec §6.6).</summary>
internal sealed class ReadSkillTool(SkillStore skills, PersonaStore personas, string callerPersonaName, IPromptSource prompts) : IAppTool
{
    private const string SkillMdFileName = "SKILL.md";

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "read_skill";

    public string Description => prompts.Render("tool.readSkill.description", NoValues);

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string" },
            ["file"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "name" },
    };

    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var name = (string?)arguments["name"];
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult("The 'name' argument is required and must be the Skill's name.");
        }

        // Holding a Skill is checked live, against the CURRENT Persona entry, never a list captured
        // at construction: a Skill removed from a Persona's frontmatter is refused on the very next
        // call, even before the restart that follows lands (Spec §6.6 Implementation notes).
        var entry = personas.ResolveByNameOrAlias(callerPersonaName);
        var heldSkills = entry?.Skills ?? [];
        if (!heldSkills.Contains(name, StringComparer.Ordinal))
        {
            return Task.FromResult(heldSkills.Count == 0
                ? $"You do not hold the Skill '{name}'. You hold no Skills."
                : $"You do not hold the Skill '{name}'. Your Skills: {string.Join(", ", heldSkills)}.");
        }

        var skill = skills.Get(name);
        if (skill is null)
        {
            return Task.FromResult($"The Skill '{name}' does not exist.");
        }

        var file = (string?)arguments["file"];
        if (string.IsNullOrWhiteSpace(file))
        {
            file = SkillMdFileName;
        }

        var text = skills.ReadFile(name, file);
        if (text is null)
        {
            return Task.FromResult($"The Skill '{name}' has no file '{file}'. Its files: {string.Join(", ", skill.Files)}.");
        }

        // The model already has the Skill's name and description (from the Skill Index, or from
        // list_agents' job description); the frontmatter block that repeats them serves no purpose
        // in a tool result and is stripped. Only SKILL.md carries frontmatter at all, so this is a
        // no-op for every other file.
        if (string.Equals(file, SkillMdFileName, StringComparison.Ordinal))
        {
            (_, text) = PersonaFrontmatter.Parse(text);
        }

        var others = skill.Files.Where(other => !string.Equals(other, file, StringComparison.Ordinal)).ToList();
        var also = others.Count > 0 ? $" · also: {string.Join(", ", others)}" : string.Empty;

        return Task.FromResult($"[Skill: {name} · file: {file}{also}]\n\n{text}");
    }
}
