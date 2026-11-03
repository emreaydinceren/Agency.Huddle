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

    /// <summary>Where the Second Brain Skills (<c>ingest</c>, <c>wrap-up</c>, <c>brain-check</c>) are embedded.</summary>
    private const string OverlayResourcePrefix = "Overlays/second-brain/skills/";

    /// <summary>Every shipped Skill's file name → text, keyed by Skill name; both levels compare ordinally.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> All { get; } = BuildAll();

    /// <summary>Reads the embedded files under <see cref="ResourcePrefix"/> and <see cref="OverlayResourcePrefix"/> (Spec §6.8).</summary>
    /// <returns>Each file's path after its prefix (<c>skill/file</c>) → text; ordinal keys.</returns>
    private static Dictionary<string, string> ReadPrefixes()
    {
        Dictionary<string, string> all = new(StringComparer.Ordinal);
        foreach (string prefix in (string[])[ResourcePrefix, OverlayResourcePrefix])
        {
            foreach ((string remainder, string text) in EmbeddedResources.ReadFolder(
                typeof(SkillCatalog).Assembly,
                prefix,
                static remainder => remainder.Contains('/')))
            {
                all[remainder] = text;
            }
        }

        return all;
    }

    /// <summary>
    /// Enumerates the assembly's embedded resources under both prefixes, splits each
    /// logical name into a Skill name and a file name, and reads every file's text with line endings
    /// normalised to <c>\n</c>.
    /// </summary>
    /// <returns>An immutable Skill name → (file name → text) map, ordinal at both levels.</returns>
    private static FrozenDictionary<string, IReadOnlyDictionary<string, string>> BuildAll()
    {
        Dictionary<string, Dictionary<string, string>> bySkill = new(StringComparer.Ordinal);

        foreach ((string remainder, string text) in ReadPrefixes())
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
