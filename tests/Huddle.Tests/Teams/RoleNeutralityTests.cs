using System.Text.Json;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins NF-11 (Task 83): app code does not learn a role. Every <c>.cs</c> and <c>.razor</c> file under <c>src/Huddle.App</c>
/// is scanned for the role titles and role ids of both built-in team type manifests, so a role cannot be hard-coded
/// instead of read from a manifest. The few files that legitimately name a role are listed in <c>neutrality-allow.txt</c>,
/// which may shrink but never grow.
/// </summary>
public sealed class RoleNeutralityTests
{
    private const string AllowListFileName = "neutrality-allow.txt";

    private const int AllowListCeiling = 14;

    private static readonly string[] BuiltinTypeIds = ["development", "household"];

    /// <summary>No source file outside the allow-list contains a role title or role id from a built-in manifest.</summary>
    [Fact]
    public void AppSources_OutsideAllowList_NameNoRole()
    {
        string root = RepoRoot();
        List<string> roleNames = RoleNames(root);
        HashSet<string> allowed = new(AllowedFiles(), StringComparer.OrdinalIgnoreCase);
        List<string> offenders = [];

        foreach (string file in SourceFiles(root))
        {
            string relative = Relative(root, file);
            if (allowed.Contains(relative))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            string? hit = roleNames.FirstOrDefault(name => text.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                offenders.Add($"{relative}: {hit}");
            }
        }

        Assert.Equal(string.Empty, string.Join('\n', offenders));
    }

    /// <summary>The allow-list is not longer than the number checked in, and each entry names an existing file.</summary>
    [Fact]
    public void AllowList_NeverGrows_AndNamesExistingFiles()
    {
        string root = RepoRoot();
        List<string> allowed = AllowedFiles();

        Assert.True(allowed.Count <= AllowListCeiling, $"The allow-list has {allowed.Count} entries; the ceiling is {AllowListCeiling}.");
        Assert.Equal(string.Empty, string.Join('\n', allowed.Where(path => !File.Exists(Path.Combine(root, path)))));
    }

    /// <summary>The scan reads the role titles and ids of both manifests, so it cannot pass by finding nothing to look for.</summary>
    [Fact]
    public void RoleNames_ReadFromBothManifests_ListTitlesAndIds()
    {
        List<string> names = RoleNames(RepoRoot());

        Assert.Equal(
            [
                "Development Manager", "development-manager", "Tech Lead", "tech-lead", "Developer", "Reviewer",
                "Security Engineer", "security-engineer", "QA Engineer", "qa-engineer", "Release Engineer", "release-engineer",
                "Retro Agent", "retro-agent", "Household Manager", "household-manager", "School Liaison", "school-liaison",
            ],
            names);
    }

    /// <summary>Finds the repository root by walking up to <c>Huddle.slnx</c>.</summary>
    /// <returns>The repository root.</returns>
    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Huddle.slnx was not found above the test folder.");
    }

    /// <summary>Reads every role id from the built-in manifests and the title of the hiring-pool job each role names.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The names to look for.</returns>
    private static List<string> RoleNames(string root)
    {
        string poolPath = Path.Combine(root, "src", "Huddle.App", "HiringPool", "hiringpool.json");
        using JsonDocument pool = JsonDocument.Parse(File.ReadAllText(poolPath));
        Dictionary<string, string> titles = pool.RootElement.GetProperty("jobs").EnumerateArray()
            .ToDictionary(
                static job => job.GetProperty("id").GetString() ?? string.Empty,
                static job => job.GetProperty("title").GetString() ?? string.Empty,
                StringComparer.Ordinal);
        List<string> names = [];
        foreach (string typeId in BuiltinTypeIds)
        {
            string path = Path.Combine(root, "src", "Huddle.App", "TeamTypes", "Builtin", typeId, "team-type.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonElement role in document.RootElement.GetProperty("roles").EnumerateArray())
            {
                string id = role.GetProperty("id").GetString() ?? string.Empty;
                string job = role.TryGetProperty("job", out JsonElement named) ? named.GetString() ?? id : id;
                names.Add(titles.GetValueOrDefault(job, string.Empty));
                names.Add(id);
            }
        }

        return [.. names.Where(static name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Lists the source files the scan reads: <c>.cs</c> and <c>.razor</c> under the app, outside build output and the built-in team types.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The file paths.</returns>
    private static List<string> SourceFiles(string root)
    {
        string appRoot = Path.Combine(root, "src", "Huddle.App");
        return [.. Directory.EnumerateFiles(appRoot, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                string relative = Relative(root, path);
                return !relative.Contains("/obj/", StringComparison.Ordinal)
                    && !relative.Contains("/bin/", StringComparison.Ordinal)
                    && !relative.StartsWith("src/Huddle.App/TeamTypes/Builtin/", StringComparison.Ordinal);
            })];
    }

    /// <summary>Reads the allow-list: one repository-relative path per line, a <c>#</c> starting the reason.</summary>
    /// <returns>The allowed paths.</returns>
    private static List<string> AllowedFiles()
    {
        string path = Path.Combine(RepoRoot(), "tests", "Huddle.Tests", "Teams", AllowListFileName);
        return [.. File.ReadAllLines(path)
            .Select(static line => line.Split('#')[0].Trim())
            .Where(static line => line.Length > 0)];
    }

    /// <summary>Makes <paramref name="path"/> relative to <paramref name="root"/> with forward slashes.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="path">A path under it.</param>
    /// <returns>The relative path.</returns>
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
