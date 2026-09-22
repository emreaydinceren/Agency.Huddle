using System.Collections.Frozen;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Computes which App Tools one session should be offered, from the full tool catalog and the
/// Persona's resolved Skills (Spec §6.5). Assigning a Skill is the only way a Persona gains one of
/// <see cref="Grantable"/>; gating is defined here, in code, never by a Skill file, so a Skill can
/// only ever add one of these two tools and can never take away, or add, anything else.
/// </summary>
internal static class SkillGrants
{
    /// <summary>The name of the tool that reads a Skill's body or a supporting file (Spec §6.6).</summary>
    private const string ReadSkillToolName = "read_skill";

    /// <summary>The name of the tool that checks a Candidate roster against Spec §8.3.</summary>
    private const string ValidateTeammateToolName = "validate_teammate";

    /// <summary>The name of the tool that proposes new Teammates to the Human (Spec §6.8).</summary>
    private const string ProposeTeammatesToolName = "propose_teammates";

    /// <summary>Tools that exist only for Personas holding a Skill that lists them (Spec §6.5).</summary>
    internal static readonly FrozenSet<string> Grantable =
        FrozenSet.ToFrozenSet([ValidateTeammateToolName, ProposeTeammatesToolName], StringComparer.Ordinal);

    /// <summary>
    /// Filters <paramref name="all"/> down to the tools this session should be offered:
    /// <see cref="ReadSkillToolName"/> only when <paramref name="skills"/> is non-empty, each
    /// <see cref="Grantable"/> tool only when some held Skill lists it, and every other tool always -
    /// all in <paramref name="all"/>'s original order, so <c>GetHelpTool</c> built from the result and
    /// <c>tools/list</c> agree.
    /// </summary>
    /// <param name="all">The full tool catalog for this session, before Skill gating.</param>
    /// <param name="skills">The Persona's resolved Skills for this session.</param>
    /// <returns>The tools to register with the Agent, in <paramref name="all"/>'s order.</returns>
    internal static IReadOnlyList<IAppTool> Offer(IReadOnlyList<IAppTool> all, IReadOnlyList<Skill> skills)
    {
        HashSet<string> granted = new(StringComparer.Ordinal);
        foreach (var skill in skills)
        {
            foreach (var tool in skill.Tools)
            {
                granted.Add(tool);
            }
        }

        List<IAppTool> offered = [];
        foreach (var tool in all)
        {
            bool include = tool.Name switch
            {
                ReadSkillToolName => skills.Count > 0,
                _ when Grantable.Contains(tool.Name) => granted.Contains(tool.Name),
                _ => true,
            };

            if (include)
            {
                offered.Add(tool);
            }
        }

        return offered;
    }
}
