using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Writes a Persona/Teammate definition file directly to disk through
/// <see cref="TeammatePaths.DefinitionFile(string)"/>, the one path unit tests should use instead of
/// combining <see cref="TeammatePaths.DefinitionsRoot"/> themselves, so a later change to the
/// on-disk layout (Spec §6.15) touches this helper only.
/// </summary>
internal static class TestPersonaFiles
{
    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="stem"/>'s definition file under
    /// <paramref name="paths"/>, creating its containing folder first.
    /// </summary>
    /// <param name="paths">Resolves the definition file's path.</param>
    /// <param name="stem">The Persona/Teammate's file stem (its Alias), e.g. "coo".</param>
    /// <param name="text">The definition file's full text (frontmatter and system prompt).</param>
    public static void Write(TeammatePaths paths, string stem, string text)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(text);

        string file = paths.DefinitionFile(stem);
        Directory.CreateDirectory(paths.DefinitionsRoot);
        File.WriteAllText(file, text);
    }
}
