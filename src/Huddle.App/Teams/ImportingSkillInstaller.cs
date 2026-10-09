using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Makes a Team's skills present before its members start (HR-9): a skill the <see cref="SkillStore"/> already
/// holds, or a builtin one, is left alone; any other is imported from its owner's catalog entry. A skill whose
/// review shows more than a missing license is not installed, because the user must consent to scripts (NF-4);
/// it is returned so Start Team stops and the user imports it from Settings.
/// </summary>
/// <param name="store">The skills on disk.</param>
/// <param name="catalog">The skills catalog the entries are found in.</param>
/// <param name="importer">Previews and installs a catalog skill.</param>
/// <param name="logger">Receives the reason a skill could not be installed.</param>
internal sealed class ImportingSkillInstaller(SkillStore store, SkillsCatalogIndex catalog, ISkillImporter importer, ILogger<ImportingSkillInstaller> logger) : ISkillInstaller
{
    private const string BuiltinSource = "builtin";

    private const string GitHubPrefix = "https://github.com/";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkillRef>> EnsureInstalledAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(skills);

        List<SkillRef> missing = [];
        foreach (SkillRef skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill.Name)
                || (!string.Equals(skill.Source, ImportingSkillInstaller.BuiltinSource, StringComparison.Ordinal)
                    && store.Get(skill.Name) is null
                    && !await this.ImportAsync(skill, ct).ConfigureAwait(false)))
            {
                missing.Add(skill);
            }
        }

        return missing;
    }

    /// <summary>Tells whether a flag list holds nothing a user must consent to.</summary>
    private static bool OnlyLicenseFlags(IReadOnlyList<ReviewFlag> flags)
    {
        return flags.All(static f => f.Kind == ReviewFlagKind.NoLicense);
    }

    /// <summary>Finds, previews and installs one skill.</summary>
    private async Task<bool> ImportAsync(SkillRef skill, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(skill.Path))
        {
            logger.LogWarning("Skill {Skill} names no path in {Source}.", skill.Name, skill.Source);
            return false;
        }

        string repo = skill.Source.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? skill.Source : GitHubPrefix + skill.Source;
        SkillsCatalogEntry? entry = catalog.FindByRepoPath(repo, skill.Path);
        if (entry is null)
        {
            logger.LogWarning("Skill {Skill} is not in the catalog for {Source}.", skill.Name, skill.Source);
            return false;
        }

        ImportPreview? preview = await importer.PreviewFromCatalogAsync(entry.Id, ct).ConfigureAwait(false);
        if (preview is null || !preview.CanInstall)
        {
            logger.LogWarning("Skill {Skill} could not be previewed for install.", skill.Name);
            return false;
        }

        if (!string.Equals(preview.Name, skill.Name, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Skill {Skill} resolved to a different skill, {PreviewName}; not installing it.", skill.Name, preview.Name);
            return false;
        }

        if (!ImportingSkillInstaller.OnlyLicenseFlags(preview.Flags))
        {
            logger.LogWarning("Skill {Skill} needs review before it can be installed.", skill.Name);
            return false;
        }

        ImportOutcome outcome = importer.Install(preview);
        if (!outcome.Installed)
        {
            logger.LogWarning("Skill {Skill} was not installed: {Error}", skill.Name, outcome.Error);
        }

        return outcome.Installed;
    }
}
