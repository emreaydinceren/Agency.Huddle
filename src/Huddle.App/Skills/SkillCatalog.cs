using System.Collections.Frozen;
using System.Reflection;

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
        Assembly assembly = typeof(SkillCatalog).Assembly;
        Dictionary<string, Dictionary<string, string>> bySkill = new(StringComparer.Ordinal);

        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            string normalizedName = resourceName.Replace('\\', '/');
            if (!normalizedName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string remainder = normalizedName[ResourcePrefix.Length..];
            int separatorIndex = remainder.IndexOf('/');
            if (separatorIndex < 0)
            {
                continue;
            }

            string skillName = remainder[..separatorIndex];
            string fileName = remainder[(separatorIndex + 1)..];
            string text = ReadResourceText(assembly, resourceName);

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

    /// <summary>Reads one embedded resource's full text as UTF-8, with line endings normalised to <c>\n</c>.</summary>
    /// <param name="assembly">The assembly the resource is embedded in.</param>
    /// <param name="resourceName">The resource's manifest name.</param>
    /// <returns>The resource's text.</returns>
    /// <exception cref="InvalidOperationException">The named resource is not embedded in <paramref name="assembly"/>.</exception>
    private static string ReadResourceText(Assembly assembly, string resourceName)
    {
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' was enumerated but could not be opened.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
