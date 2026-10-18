using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.Packs;

/// <summary>The data folder, the context and the Skill Sources folder one run uses.</summary>
/// <param name="DataDir">Huddle's data folder.</param>
/// <param name="Context">What the checks need, from the context file.</param>
/// <param name="SkillSourcesDir">The Skill Sources folder.</param>
internal sealed record DataLocation(string DataDir, CheckContext Context, string SkillSourcesDir);

/// <summary>The outcome of finding the data: the location, or the exit-2 message.</summary>
/// <param name="Location">The location, or null.</param>
/// <param name="Error">The message for standard error, or null.</param>
internal sealed record LocateResult(DataLocation? Location, string? Error);

/// <summary>Finds the data folder, reads the context file and works out the Skill Sources folder (§6.4).</summary>
internal static class DataLocator
{
    /// <summary>The environment variable that names the data folder.</summary>
    public const string EnvironmentVariable = "HUDDLE_DATA_DIR";

    private const string DefaultFolder = "App_Data";

    /// <summary>The data folder: <c>--data-dir</c>, then <c>HUDDLE_DATA_DIR</c>, then the first <c>App_Data</c> under the
    /// current directory or beside the executable that holds a context file, else the first of those.</summary>
    public static string DataDir(string? option, Func<string, string?> environment, string currentDirectory, string executableDirectory, IFileReader files)
    {
        string? given = option ?? Blank(environment(EnvironmentVariable));
        if (given is not null)
        {
            return Path.GetFullPath(given, currentDirectory);
        }

        string[] candidates = [Path.Combine(currentDirectory, DefaultFolder), Path.Combine(executableDirectory, DefaultFolder)];
        return candidates.FirstOrDefault(candidate => files.FileExists(Path.Combine(candidate, InstallContext.FileName))) ?? candidates[0];
    }

    /// <summary>Reads the context file in <paramref name="dataDir"/>.</summary>
    public static LocateResult Locate(string dataDir, IFileReader files)
    {
        if (!files.DirectoryExists(dataDir))
        {
            return new LocateResult(null, $"Cannot read data folder {dataDir}: it does not exist.");
        }

        string contextPath = Path.Combine(dataDir, InstallContext.FileName);
        if (!files.FileExists(contextPath))
        {
            return new LocateResult(null, $"No install-context.json in {dataDir}. Start Huddle once so it writes it, then run this again.");
        }

        InstallContextReadResult read = InstallContext.Parse(files.ReadAllText(contextPath));
        if (read.NewerSchemaVersion is { } newer)
        {
            return new LocateResult(null, $"install-context.json was written for schemaVersion {newer}; this huddle-packs checks {InstallContext.KnownSchemaVersion}. Use the huddle-packs from InstallSkillPack.md.");
        }

        if (read.Value is not { } context)
        {
            return new LocateResult(null, $"install-context.json in {dataDir} cannot be read: {read.Reason}. Start Huddle once so it writes it again, then run this again.");
        }

        string skillSources = string.IsNullOrWhiteSpace(context.SkillSourcesDir) ? Path.Combine(dataDir, "SkillSources") : context.SkillSourcesDir;
        CheckContext checkContext = CheckContext.From(context) with { SkillSourcesDir = skillSources };
        return new LocateResult(new DataLocation(dataDir, checkContext, skillSources), null);
    }

    /// <summary>Null for null or white space.</summary>
    private static string? Blank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
