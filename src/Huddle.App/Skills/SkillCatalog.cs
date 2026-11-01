using System.Collections.Frozen;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Holds every shipped Skill's files as compiled-in defaults (Spec §6.1), so an install with an
/// empty <c>{DataDir}</c> still has <c>team-building</c>, and an upgrade delivers improved text
/// without touching disk.
/// </summary>
/// <remarks>
/// Built once, from the assembly's own embedded resources under the <c>Skills/Defaults/</c>
/// logical-name prefix (see <c>Huddle.App.csproj</c>'s <c>EmbeddedResource</c> entry). A shipped
/// Skill's validity is pinned by <c>SkillCatalogTests</c>, never checked here at run time.
/// </remarks>
internal static class SkillCatalog
{
    private const string ResourcePrefix = "Skills/Defaults/";

    /// <summary>Every shipped Skill's file name → text, keyed by Skill name; both levels compare ordinally.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } = BuildAll();

    /// <summary>
    /// Enumerates the assembly's embedded resources under <see cref="ResourcePrefix"/>, splits each
    /// logical name into a Skill name and a file name, and reads every file's text with line endings
    /// normalised to <c>\n</c>.
    /// </summary>
    /// <returns>An immutable Skill name → (file name → text) map, ordinal at both levels.</returns>
    private static FrozenDictionary<string, IReadOnlyDictionary<string, string>> BuildAll()
    {
        Dictionary<string, Dictionary<string, string>> bySkill = new(StringComparer.Ordinal);

        Dictionary<string, string> resources = EmbeddedResources.ReadFolder(
            typeof(SkillCatalog).Assembly,
            ResourcePrefix,
            static remainder => remainder.Contains('/'));
        foreach ((string remainder, string text) in resources)
        {
            int separatorIndex = remainder.IndexOf('/');
            string skillName = remainder[..separatorIndex];
            string fileName = remainder[(separatorIndex + 1)..];

            if (!bySkill.TryGetValue(skillName, out Dictionary<string, string>? files))
            {
                files = new Dictionary<string, string>(StringComparer.Ordinal);
                bySkill[skillName] = files;
            }

            files[fileName] = text;
        }

        return bySkill.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string>)pair.Value.ToFrozenDictionary(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
