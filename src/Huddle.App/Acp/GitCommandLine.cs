using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Reads the git commands out of a shell line for the merge gate (issues #396, #401): what <see cref="WorkDirPermissionHandler"/>
/// sends to the gate and what <see cref="Teams.ProcessMergeGate"/> holds for the Human's Accept. It skips leading
/// <c>NAME=value</c> words, unwraps <c>bash -c</c>, <c>sh -c</c>, <c>pwsh -Command</c> and <c>cmd /c</c>, and drops
/// redirections (<c>2&gt;&amp;1</c>), so a spelling around the command does not hide it. It is a guard against an Agent's
/// ordinary mistake, not a sandbox: aliases, scripts, <c>$(…)</c> and backslash escapes still pass.
/// </summary>
internal static partial class GitCommandLine
{
    private const int MaxDepth = 3;

    private static readonly string[] Shells = ["bash", "sh", "zsh", "dash", "pwsh", "powershell", "cmd"];

    private static readonly string[] ShellFlags = ["-c", "-lc", "-ic", "-command", "/c"];

    private static readonly string[] Wrappers = ["env", "sudo", "command", "nohup", "time"];

    private static readonly string[] MergeValued = ["-m", "-F", "-X", "-s", "--message", "--file", "--strategy", "--strategy-option"];

    private static readonly string[] PushValued = ["-o", "--push-option", "--receive-pack", "--exec", "--repo"];

    private static readonly string[] PullValued = ["-s", "-X", "--strategy", "--strategy-option", "--depth", "-o", "--upload-pack"];

    private static readonly string[] MainNames = ["main", "master", "origin/main", "origin/master", "refs/heads/main", "refs/heads/master"];

    /// <summary>One git command: its subcommand, lower-cased, and its positional words (options and their values removed).</summary>
    /// <param name="Subcommand">The subcommand, for example <c>merge</c>.</param>
    /// <param name="Args">The positional words after it; <c>--all</c> and <c>--mirror</c> are kept for a push.</param>
    internal sealed record Call(string Subcommand, IReadOnlyList<string> Args)
    {
        /// <summary>Whether the command is <c>git merge</c> or <c>git push</c>, which the gate is asked about.</summary>
        internal bool IsMergeOrPush => this.Subcommand is "merge" or "push";

        /// <summary>
        /// Whether the command would put work on main: a merge of something other than main (<c>git merge -</c> included),
        /// a pull naming a branch other than main, or a push to main, to <c>HEAD</c> (the current branch is not known
        /// here, so it counts as main) or to no named branch.
        /// </summary>
        internal bool WouldPutWorkOnMain => this.Subcommand switch
        {
            "merge" => !this.Args.All(IsMainBranch),
            "pull" => this.Args.Count >= 2 && this.Args.Skip(1).Any(static arg => !IsMainBranch(arg.TrimStart('+'))),
            "push" => this.PushesToMain(),
            _ => false,
        };

        private bool PushesToMain() =>
            this.Args.Contains("--all", StringComparer.Ordinal)
            || this.Args.Contains("--mirror", StringComparer.Ordinal)
            || this.Args.Count < 2
            || this.Args.Skip(1).Any(IsMainRef);
    }

    /// <summary>Reads every git command in a shell line, including those inside <c>bash -c "…"</c>.</summary>
    /// <param name="command">The shell line.</param>
    /// <returns>The git commands, in order.</returns>
    internal static List<Call> Parse(string command)
    {
        List<Call> calls = [];
        Collect(command, 0, calls);
        return calls;
    }

    /// <summary>Whether the line holds a command the gate must be asked about: a merge, a push, or a pull that merges a branch other than main.</summary>
    /// <param name="command">The shell line.</param>
    internal static bool NeedsGate(string command) =>
        Parse(command).Exists(static call => call.IsMergeOrPush || (call.Subcommand == "pull" && call.WouldPutWorkOnMain));

    /// <summary>Whether the name is main or master, local or on origin.</summary>
    /// <param name="name">The branch name as typed.</param>
    internal static bool IsMainBranch(string name) => MainNames.Contains(name, StringComparer.Ordinal);

    /// <summary>Whether a push refspec targets main or master (or <c>HEAD</c>), as <c>main</c>, <c>+main</c>, <c>HEAD:main</c> or <c>refs/heads/main</c>.</summary>
    /// <param name="refspec">The push argument.</param>
    internal static bool IsMainRef(string refspec)
    {
        string target = refspec.TrimStart('+');
        target = target[(target.LastIndexOf(':') + 1)..];
        return IsMainBranch(target) || target is "HEAD" or "@";
    }

    private static void Collect(string command, int depth, List<Call> calls)
    {
        foreach (List<string> raw in WorkDirPermissionHandler.SplitSegments(command))
        {
            List<string> words = CleanWords(raw);
            if (words.Count == 0)
            {
                continue;
            }

            if (depth < MaxDepth && ShellScript(words) is { } inner)
            {
                Collect(inner, depth + 1, calls);
            }
            else if (ReadGit(words) is { } call)
            {
                calls.Add(call);
            }
        }
    }

    /// <summary>Drops redirections everywhere and leading <c>NAME=value</c> words and wrappers such as <c>env</c> and <c>sudo</c>.</summary>
    private static List<string> CleanWords(List<string> raw)
    {
        List<string> words = [];
        int index = 0;
        while (index < raw.Count)
        {
            string word = raw[index];
            index += RedirectionOnly().IsMatch(word) ? 2 : 1;
            if (!Redirection().IsMatch(word) && !(words.Count == 0 && (Assignment().IsMatch(word) || Wrappers.Contains(word, StringComparer.Ordinal))))
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>The script a shell word list runs (<c>bash -c "…"</c>), or <see langword="null"/> when the line is not a shell with a script.</summary>
    private static string? ShellScript(List<string> words)
    {
        string name = words[0][(words[0].LastIndexOfAny(['/', '\\']) + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        if (!Shells.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        int flag = words.FindIndex(1, static word => ShellFlags.Contains(word, StringComparer.OrdinalIgnoreCase));
        return flag < 0 ? null : string.Join(' ', words.Skip(flag + 1));
    }

    private static Call? ReadGit(List<string> words)
    {
        if (words.Count < 2 || !WorkDirPermissionHandler.IsGit(words[0]))
        {
            return null;
        }

        int index = 1;
        while (index < words.Count && words[index].StartsWith('-') && words[index] != "-")
        {
            index += WorkDirPermissionHandler.GitOptionsWithValue.Contains(words[index], StringComparer.Ordinal) ? 2 : 1;
        }

        if (index >= words.Count)
        {
            return null;
        }

        string subcommand = words[index].ToLowerInvariant();
        return new Call(subcommand, Positional(words, index + 1, subcommand));
    }

    private static List<string> Positional(List<string> words, int from, string subcommand)
    {
        string[] valued = subcommand switch
        {
            "merge" => MergeValued,
            "push" => PushValued,
            "pull" => PullValued,
            _ => [],
        };

        List<string> args = [];
        int index = from;
        while (index < words.Count)
        {
            string word = words[index];
            index += !word.StartsWith('-') || word == "-" || (subcommand == "push" && word is "--all" or "--mirror") || !valued.Contains(word, StringComparer.Ordinal) ? 1 : 2;
            if (word == "-" || !word.StartsWith('-') || (subcommand == "push" && word is "--all" or "--mirror"))
            {
                args.Add(word);
            }
        }

        return args;
    }

    [GeneratedRegex(@"^\d*[<>]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RedirectionOnly();

    [GeneratedRegex(@"^\d*[<>]", RegexOptions.CultureInvariant)]
    private static partial Regex Redirection();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*=", RegexOptions.CultureInvariant)]
    private static partial Regex Assignment();
}
