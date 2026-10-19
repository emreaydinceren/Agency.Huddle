using System.Globalization;
using System.Text;

namespace Agency.Huddle.Tests.Ui.Conventions;

/// <summary>
/// Finds the files each convention rule covers, scans them with <see cref="UiConventionScanner"/>,
/// maps every file to its ownership area (plan section 3.4), reads the baseline and exception files,
/// and renders baselines. The renderer is the baseline generator: a test-project helper, not a script.
/// </summary>
internal static class UiConventionSource
{
    /// <summary>The eight baseline areas, in file order.</summary>
    public static readonly IReadOnlyList<string> Areas =
        ["Shell", "Room", "Tasks", "Library", "Teams", "Settings", "Shared", "Css"];

    private const string AppRoot = "src/Huddle.App/";

    private const string ComponentsRoot = "src/Huddle.App/Components/";

    private static readonly string[] CssTrackFiles =
    [
        "src/Huddle.App/wwwroot/app.css",
        "src/Huddle.App/Components/Layout/MainLayout.razor.css",
        "src/Huddle.App/Components/Layout/ReconnectModal.razor.css",
        "src/Huddle.App/Components/Teams/TeamsNav.razor.css",
        "src/Huddle.App/Components/Shared/HuddleSegmentedControl.razor.css",
    ];

    private static readonly (string Prefix, string Area)[] AreaPrefixes =
    [
        ("Layout/", "Shell"),
        ("NeedsYou/", "Shell"),
        ("Shared/RoomList", "Shell"),
        ("Shared/RoomNavRow", "Shell"),
        ("Shared/NewChat", "Shell"),
        ("Shared/ArchivedChats", "Shell"),
        ("Pages/Chat", "Room"),
        ("Shared/MessageList", "Room"),
        ("Shared/MessageDisplay", "Room"),
        ("Shared/RendererDispatch", "Room"),
        ("Shared/Composer", "Room"),
        ("Shared/InviteTeammate", "Room"),
        ("Shared/EditPreview", "Room"),
        ("Shared/ElicitationCard", "Room"),
        ("Shared/ProposalCard", "Room"),
        ("Shared/QuestionCard", "Room"),
        ("Shared/RetroProposalCard", "Room"),
        ("Shared/SprintProposalCard", "Room"),
        ("References/", "Room"),
        ("Pages/Tasks", "Tasks"),
        ("Tasks/", "Tasks"),
        ("Search/TaskSearchCard", "Tasks"),
        ("Pages/Library", "Library"),
        ("Library/", "Library"),
        ("Pages/Team", "Teams"),
        ("Teams/", "Teams"),
        ("Pages/Settings", "Settings"),
        ("Settings/", "Settings"),
    ];

    /// <summary>Whether <paramref name="rule"/> scans the file at <paramref name="path"/> (plan section 3.2 scopes).</summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="path">Repository-relative path, forward slashes.</param>
    public static bool InScope(string rule, string path)
    {
        bool razor = path.EndsWith(".razor", StringComparison.Ordinal);
        bool csharp = path.EndsWith(".cs", StringComparison.Ordinal);
        bool components = path.StartsWith(ComponentsRoot, StringComparison.Ordinal);
        return rule switch
        {
            "CV4" => razor || csharp,
            "CV7" or "CV8" => components && (razor || csharp),
            "CV11" => string.Equals(path, "src/Huddle.App/wwwroot/app.css", StringComparison.Ordinal)
                || (components && path.EndsWith(".razor.css", StringComparison.Ordinal)),
            _ => components && razor,
        };
    }

    /// <summary>The ownership area (plan section 3.4) whose baseline file holds <paramref name="path"/>.</summary>
    /// <param name="path">Repository-relative path, forward slashes.</param>
    public static string AreaOf(string path)
    {
        if (CssTrackFiles.Contains(path, StringComparer.Ordinal))
        {
            return "Css";
        }

        if (!path.StartsWith(ComponentsRoot, StringComparison.Ordinal))
        {
            return "Shared";
        }

        string inner = path[ComponentsRoot.Length..];
        if (!inner.Contains('/'))
        {
            return "Shell";
        }

        foreach ((string prefix, string area) in AreaPrefixes)
        {
            if (inner.StartsWith(prefix, StringComparison.Ordinal))
            {
                return area;
            }
        }

        return "Shared";
    }

    /// <summary>Scans the source tree and returns every file and rule that has at least one violation.</summary>
    public static IReadOnlyList<RuleCount> ScanRepo()
    {
        string appDirectory = CssSource.RepoPath("src", "Huddle.App");
        List<RuleCount> results = [];
        foreach (string file in Directory.EnumerateFiles(appDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string path = AppRoot + Path.GetRelativePath(appDirectory, file).Replace('\\', '/');
            if (path.Contains("/bin/", StringComparison.Ordinal) || path.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }

            string? text = null;
            foreach (string rule in UiConventionScanner.Rules)
            {
                if (!InScope(rule, path))
                {
                    continue;
                }

                text ??= File.ReadAllText(file);
                RuleCount found = UiConventionScanner.Scan(rule, path, text);
                if (found.Count > 0)
                {
                    results.Add(found);
                }
            }
        }

        return results;
    }

    /// <summary>The key a rule and path share in the baseline dictionaries.</summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="path">Repository-relative path.</param>
    public static string Key(string rule, string path) => $"{rule}|{path}";

    /// <summary>Reads every <c>Baseline/&lt;Area&gt;.txt</c> into key to allowed count.</summary>
    /// <param name="baselineDirectory">The folder holding the baseline files.</param>
    public static Dictionary<string, int> ReadBaselines(string baselineDirectory)
    {
        Dictionary<string, int> allowed = new(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(baselineDirectory, "*.txt").Order(StringComparer.Ordinal))
        {
            if (string.Equals(Path.GetFileName(file), "Exceptions.txt", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string line in File.ReadLines(file).Where(static l => l.Trim().Length > 0 && !l.StartsWith('#')))
            {
                string[] parts = line.Split('|');
                allowed[Key(parts[0], parts[1])] = int.Parse(parts[2], CultureInfo.InvariantCulture);
            }
        }

        return allowed;
    }

    /// <summary>Reads <c>Baseline/Exceptions.txt</c> (<c>CVn|path|count|reason</c>) into key to extra allowed count.</summary>
    /// <param name="baselineDirectory">The folder holding the baseline files.</param>
    public static Dictionary<string, int> ReadExceptions(string baselineDirectory)
    {
        Dictionary<string, int> allowed = new(StringComparer.Ordinal);
        string file = Path.Combine(baselineDirectory, "Exceptions.txt");
        foreach (string line in File.ReadLines(file).Where(static l => l.Trim().Length > 0 && !l.StartsWith('#')))
        {
            string[] parts = line.Split('|');
            allowed[Key(parts[0], parts[1])] = allowed.GetValueOrDefault(Key(parts[0], parts[1])) + int.Parse(parts[2], CultureInfo.InvariantCulture);
        }

        return allowed;
    }

    /// <summary>Violations beyond what the baseline plus exceptions allow, one message per rule and file.</summary>
    /// <param name="actual">What the scanner found.</param>
    /// <param name="baseline">Baseline counts by key.</param>
    /// <param name="exceptions">Exception counts by key.</param>
    public static IReadOnlyList<string> Overruns(
        IReadOnlyList<RuleCount> actual,
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> exceptions)
    {
        List<string> messages = [];
        foreach (RuleCount found in actual)
        {
            string key = Key(found.Rule, found.Path);
            int allowed = baseline.GetValueOrDefault(key) + exceptions.GetValueOrDefault(key);
            if (found.Count > allowed)
            {
                messages.Add($"{found.Rule}, {found.Path}, actual {found.Count}, allowed {allowed}");
            }
        }

        return messages;
    }

    /// <summary>Baseline entries that allow more than the source has, so the baseline must be lowered.</summary>
    /// <param name="actual">What the scanner found.</param>
    /// <param name="baseline">Baseline counts by key.</param>
    /// <param name="exceptions">Exception counts by key.</param>
    public static IReadOnlyList<string> Loose(
        IReadOnlyList<RuleCount> actual,
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> exceptions)
    {
        Dictionary<string, int> counts = actual.ToDictionary(static a => Key(a.Rule, a.Path), static a => a.Count, StringComparer.Ordinal);
        List<string> messages = [];
        foreach ((string key, int allowed) in baseline)
        {
            int real = Math.Max(0, counts.GetValueOrDefault(key) - exceptions.GetValueOrDefault(key));
            if (allowed > real)
            {
                messages.Add($"{key}, baseline {allowed}, source {real}");
            }
        }

        return messages;
    }

    /// <summary>Renders one baseline file text per area, lines sorted by rule number then path.</summary>
    /// <param name="actual">What the scanner found.</param>
    public static IReadOnlyDictionary<string, string> RenderBaselines(IReadOnlyList<RuleCount> actual)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (string area in Areas)
        {
            StringBuilder text = new();
            foreach (RuleCount found in actual
                .Where(a => string.Equals(AreaOf(a.Path), area, StringComparison.Ordinal))
                .OrderBy(static a => int.Parse(a.Rule[2..], CultureInfo.InvariantCulture))
                .ThenBy(static a => a.Path, StringComparer.Ordinal))
            {
                text.Append(found.Rule).Append('|').Append(found.Path).Append('|')
                    .Append(found.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            }

            files[area] = text.ToString();
        }

        return files;
    }

    /// <summary>Writes the rendered baselines into the repository's <c>Baseline</c> folder.</summary>
    /// <param name="actual">What the scanner found.</param>
    /// <param name="baselineDirectory">The folder to write the baseline files into.</param>
    public static void WriteBaselines(IReadOnlyList<RuleCount> actual, string baselineDirectory)
    {
        foreach ((string area, string text) in RenderBaselines(actual))
        {
            File.WriteAllText(Path.Combine(baselineDirectory, area + ".txt"), text);
        }
    }

}
