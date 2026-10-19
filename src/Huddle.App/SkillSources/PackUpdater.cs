using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Applies an update plan (Skill Packs spec §6.9.8, "Update does"), in this order: copy every changed and added
/// Skill into <c>Skills/</c> (one failure stops here and nothing else is written); then, for each ticked Teammate, write
/// <c>skills:</c>, <c>consult_when:</c> and <c>_packs:</c> with <see cref="PackMembership"/>, which restarts it; then one
/// <c>update-applied</c> log line. Callers rebuild the registry afterwards.</summary>
internal sealed class PackUpdater(SkillRegistry registry, ISkillCopier copier, PersonaStore personas, SourceLog log)
{
    /// <summary>Applies <paramref name="plan"/>; <paramref name="ticked"/> names the Teammates whose membership changes.</summary>
    public async Task<RemovalOutcome> ApplyAsync(PackUpdatePlan plan, IReadOnlyCollection<string> ticked, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ticked);
        ResolvedRegistry resolved = registry.Current.Resolved;
        IReadOnlyList<SkillEntry> toCopy = [.. plan.FileChanges.Select(static change => change.Name)
            .Concat(plan.Added.Select(static change => change.Name))
            .Distinct(StringComparer.Ordinal)
            .Select(resolved.FindSkill)
            .OfType<SkillEntry>()];
        CopyResult copied = await copier.CopyAsync(toCopy, ct).ConfigureAwait(false);
        if (!copied.Ok)
        {
            CopyFailure first = copied.Failed[0];
            return new RemovalOutcome(false, $"{first.Name} could not be copied: {first.Message} No Teammate was changed.");
        }

        PackEntry? pack = registry.FindPack(plan.PackId);
        int changed = 0;
        if (pack is not null)
        {
            PackContents contents = PackContents.From(pack);
            foreach (TeammateChange change in plan.Teammates.Where(change => ticked.Contains(change.Name, StringComparer.OrdinalIgnoreCase)))
            {
                if (personas.Get(change.Name) is not { } persona)
                {
                    continue;
                }

                string text = PackMembership.Add(persona.Text, contents);
                if (!string.Equals(text, persona.Text, StringComparison.Ordinal))
                {
                    _ = personas.Update(persona.Name, text, persona.Model, persona.Effort, persona.WorkMode);
                    changed++;
                }
            }
        }

        string? source = pack?.Source ?? resolved.FindSkill(plan.PackId)?.Source;
        if (source is not null)
        {
            log.Append(source, new LogLine("update-applied", pack is null ? $"skill:{plan.PackId}" : $"pack:{plan.PackId}", Data: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["teammates"] = plan.Teammates.Where(change => ticked.Contains(change.Name, StringComparer.OrdinalIgnoreCase)).Select(static change => change.Name).ToList(),
            }));
        }

        string teammates = Agency.Huddle.App.Components.Shared.Plural.Of(changed, "Teammate", "Teammates");
        return new RemovalOutcome(true, changed == 0 ? $"{plan.Title} updated." : $"{plan.Title} updated for {teammates}.");
    }
}
