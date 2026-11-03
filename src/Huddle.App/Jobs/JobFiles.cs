namespace Agency.Huddle.App.Jobs;

/// <summary>Where an owner's <c>schedule.md</c> lives.</summary>
internal static class JobFiles
{
    /// <summary>
    /// The file name, beside an Agent's definition in its Agent folder or in <c>{DataDir}/Human/</c>. <c>PersonaStore</c> exempts
    /// this one name from "an Agent folder holds only its definition", unless the folder is itself named <c>schedule</c>.
    /// </summary>
    public const string FileName = "schedule.md";

    /// <summary>The folder under the data directory holding the Human's own <c>schedule.md</c>.</summary>
    public const string HumanFolder = "Human";

    /// <summary>Whether <paramref name="file"/> is a <c>schedule.md</c> beside a definition rather than the definition of an Agent called Schedule.</summary>
    /// <param name="file">A file's full path.</param>
    /// <returns><see langword="true"/> for a Job file.</returns>
    public static bool IsSidecar(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string folder = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty);
        return string.Equals(Path.GetFileName(file), FileName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(folder, Path.GetFileNameWithoutExtension(FileName), StringComparison.OrdinalIgnoreCase);
    }
}
