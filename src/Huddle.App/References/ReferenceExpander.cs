using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.References;

/// <summary>
/// Builds the Referenced items header for one Message's text (spec section 9.1): scans the text, looks each
/// reference up now and formats what was found, within the <c>Team:References</c> limits.
/// </summary>
/// <param name="resolver">Looks each reference up now.</param>
/// <param name="prompts">Supplies the header, line, hint and more texts.</param>
/// <param name="options">Supplies <c>Team:References</c> and the Tasks feature flag.</param>
internal sealed class ReferenceExpander(ReferenceResolver resolver, IPromptSource prompts, IOptions<TeamOptions> options)
{
    private const string ToolPrefix = "mcp__team__";

    /// <summary>Expands the references in <paramref name="triggerText"/> into the header text.</summary>
    /// <param name="triggerText">The triggering Message's own text; never catch-up or Transcript text.</param>
    /// <param name="usesToolNamePrefix">Whether the Adapter names tools with the <c>mcp__team__</c> prefix.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>The header text, or null when expansion is off or the text names nothing to describe.</returns>
    public async Task<string?> ExpandAsync(string triggerText, bool usesToolNamePrefix, CancellationToken ct)
    {
        TeamOptions team = options.Value;
        if (!team.References.ExpandInPrompt)
        {
            return null;
        }

        // The header never lists Library files (the Library block does), so they are not looked up here: no disk check per Turn.
        List<ReferenceCandidate> candidates = [.. ReferenceScanner.Scan(triggerText).Where(static candidate => candidate.Kind != ReferenceKind.LibraryFile)];
        if (candidates.Count == 0)
        {
            return null;
        }

        ResolvedSet resolved = await resolver.ResolveCurrentAsync(candidates, ct);
        string prefix = usesToolNamePrefix ? ReferenceExpander.ToolPrefix : string.Empty;
        return ReferencedItemsFormatter.Format(
            resolved.Items,
            resolved.EligibleCount,
            prompts,
            team.References.EffectiveMaxPromptChars,
            prefix + "list_agents",
            team.Tasks.Enabled ? prefix + "get_task" : null);
    }
}
