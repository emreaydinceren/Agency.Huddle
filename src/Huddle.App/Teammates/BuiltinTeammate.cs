using System.Reflection;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Holds the shipped Chief of Staff Persona file as a compiled-in default (Spec §6.12), so an
/// install with an empty Persona library still has one, and an upgrade delivers improved text
/// without touching disk.
/// </summary>
/// <remarks>
/// Read once, from the assembly's own embedded resource at the <c>Builtin/chief-of-staff.md</c>
/// logical name (see <c>Huddle.App.csproj</c>'s <c>EmbeddedResource</c> entry), the same way
/// <see cref="Agency.Huddle.App.Skills.SkillCatalog"/> reads its shipped Skill files: line
/// endings are normalised to <c>\n</c>, because <see cref="Acp.PersonaFrontmatter.TryReadIdentity(string, out Acp.PersonaIdentity?, out string)"/>
/// and <see cref="Acp.PersonaFrontmatter.Parse(string)"/> both tolerate either style, and a
/// single normalised form keeps <see cref="DefaultText"/> predictable for callers that compare
/// or rewrite it.
/// </remarks>
internal static class BuiltinTeammate
{
    private const string ResourceName = "Builtin/chief-of-staff.md";

    /// <summary>
    /// The <c>_builtin</c> frontmatter value identifying the Chief of Staff (Spec §6.3, §6.12).
    /// The only value V1 gives meaning to; see <see cref="Acp.PersonaIdentity.Builtin"/>.
    /// </summary>
    internal const string ChiefOfStaffMarker = "chief-of-staff";

    /// <summary>The Chief of Staff's default Persona file text - frontmatter and Body - read once at first use.</summary>
    internal static string DefaultText { get; } = ReadDefaultText();

    /// <summary>Reads the embedded default Persona file's full text as UTF-8, with line endings normalised to <c>\n</c>.</summary>
    /// <returns>The embedded resource's text.</returns>
    /// <exception cref="InvalidOperationException">The embedded resource is missing from the assembly.</exception>
    private static string ReadDefaultText()
    {
        Assembly assembly = typeof(BuiltinTeammate).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found in the assembly.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
