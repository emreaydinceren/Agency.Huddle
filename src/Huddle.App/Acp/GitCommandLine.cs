using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Reads the git commands out of a shell line for the merge gate (issues #396, #401): what <see cref="MergeGateGuard"/>
/// sends to the gate and what <see cref="Teams.ProcessMergeGate"/> holds for the Human's Accept. It skips leading
/// <c>NAME=value</c> words, unwraps <c>bash -c</c>, <c>sh -c</c>, <c>pwsh -Command</c> and <c>cmd /c</c>, and drops
/// redirections (<c>2&gt;&amp;1</c>), so a spelling around the command does not hide it. It is a guard against an Agent's
/// ordinary mistake, not a sandbox: an alias given with <c>-c alias.x=push</c> or set with <c>git config alias.x push</c> counts as a
/// push, but aliases already in a git config, scripts, <c>$(…)</c> and backslash escapes still pass.
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

        /// <summary>
        /// Whether the command is a <c>git push</c>, whatever its arguments: a network URL, a file path, a bare push, <c>--all</c> or a feature
        /// branch. A push leaves the Team's repository, so a Needs You level never lets one pass without the Human's Accept.
        /// </summary>
        internal bool PushesWork => this.Subcommand == "push";

        /// <summary>Whether the command is a <c>git merge</c> or <c>git pull</c> that puts work on main (see <see cref="WouldPutWorkOnMain"/>), which stays in the local repository.</summary>
        internal bool MergesLocallyIntoMain => this.Subcommand is "merge" or "pull" && this.WouldPutWorkOnMain;

        /// <summary>
        /// The words a local merge or pull puts on main: for a merge the positional words that are not main (a <c>-</c> or a SHA included), for a
        /// pull those after the remote. Empty for any other command.
        /// </summary>
        internal IReadOnlyList<string> BranchesIntoMain => this.Subcommand switch
        {
            "merge" => [.. this.Args.Where(static arg => !IsMainBranch(arg))],
            "pull" => [.. this.Args.Skip(1).Select(static arg => arg.TrimStart('+')).Where(static arg => !IsMainBranch(arg))],
            _ => [],
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

    /// <summary>Whether the line holds a <c>git push</c> of any kind, including one inside <c>bash -c "…"</c>.</summary>
    /// <param name="command">The shell line.</param>
    internal static bool HasPush(string command) => Parse(command).Exists(static call => call.PushesWork);

    /// <summary>
    /// Whether the line only merges locally into main: some command merges or pulls work into main and no command pushes. A line that
    /// holds both counts as a push, which a Needs You level never releases.
    /// </summary>
    /// <param name="command">The shell line.</param>
    internal static bool OnlyLocalMergesIntoMain(string command)
    {
        List<Call> calls = Parse(command);
        return calls.Exists(static call => call.MergesLocallyIntoMain) && !calls.Exists(static call => call.PushesWork);
    }

    /// <summary>
    /// Every branch the line's local merges and pulls put on main, in order. A line that has no such command, or that also pushes, has none
    /// that matter: the caller checks <see cref="OnlyLocalMergesIntoMain"/> first.
    /// </summary>
    /// <param name="command">The shell line.</param>
    internal static List<string> BranchesMergedIntoMain(string command) =>
        [.. Parse(command).Where(static call => call.MergesLocallyIntoMain).SelectMany(static call => call.BranchesIntoMain)];

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

    /// <summary>Git options that consume the next word.</summary>
    internal static readonly string[] GitOptionsWithValue = ["-C", "-c", "--git-dir", "--work-tree", "--namespace"];

    /// <summary>
    /// Splits one shell line into commands of words. A word may be wrapped in double or single quotes (so a quoted
    /// path with spaces stays one word, and a separator inside quotes does not split); commands end at <c>;</c>,
    /// <c>&amp;</c>, <c>|</c> (so also <c>&amp;&amp;</c> and <c>||</c>), parentheses and line breaks. Still not handled:
    /// backslash escapes, <c>$(…)</c> or backtick substitution, here-documents and aliases, so a hostile spelling
    /// can still pass; the gate is a guard against honest mistakes, not a sandbox.
    /// </summary>
    /// <param name="command">The shell command line.</param>
    /// <returns>One word list per command, in order.</returns>
    internal static List<List<string>> SplitSegments(string command)
    {
        List<List<string>> segments = [];
        List<string> words = [];
        StringBuilder word = new();
        bool inWord = false;
        char quote = '\0';

        void EndWord()
        {
            if (inWord)
            {
                words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
        }

        void EndSegment()
        {
            EndWord();
            if (words.Count > 0)
            {
                segments.Add(words);
                words = [];
            }
        }

        foreach (char c in command)
        {
            if (quote != '\0' && c == quote)
            {
                quote = '\0';
            }
            else if (quote != '\0')
            {
                word.Append(c);
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                inWord = true;
            }
            else if (c is ' ' or '\t')
            {
                EndWord();
            }
            else if (c is ';' or '&' or '|' or '(' or ')' or '\r' or '\n')
            {
                EndSegment();
            }
            else
            {
                word.Append(c);
                inWord = true;
            }
        }

        EndSegment();
        return segments;
    }

    /// <summary>Whether the word names the git executable, with or without a directory or <c>.exe</c>.</summary>
    /// <param name="word">The first word of a segment, unquoted.</param>
    internal static bool IsGit(string word)
    {
        string name = word[(word.LastIndexOfAny(['/', '\\']) + 1)..];
        return string.Equals(name, "git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "git.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Strips surrounding quote characters from a shell word.</summary>
    /// <param name="word">The word.</param>
    internal static string Unquote(string word) => word.Trim('"', '\'');

    private static void Collect(string command, int depth, List<Call> calls)
    {
        foreach (List<string> raw in SplitSegments(command))
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
        if (words.Count < 2 || !IsGit(words[0]))
        {
            return null;
        }

        int index = 1;
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase);
        while (index < words.Count && words[index].StartsWith('-') && words[index] != "-")
        {
            if (words[index] == "-c" && index + 1 < words.Count && AliasDefinition().Match(words[index + 1]) is { Success: true } definition)
            {
                aliases[definition.Groups["name"].Value] = definition.Groups["value"].Value;
            }

            index += GitOptionsWithValue.Contains(words[index], StringComparer.Ordinal) ? 2 : 1;
        }

        if (index >= words.Count)
        {
            return null;
        }

        string subcommand = words[index].ToLowerInvariant();
        if (aliases.TryGetValue(words[index], out string? aliasValue) && AliasRunsPush(aliasValue))
        {
            subcommand = "push";
        }

        List<string> args = Positional(words, index + 1, subcommand);
        return subcommand == "config" && SetsPushAlias(args) ? new Call("push", []) : new Call(subcommand, args);
    }

    /// <summary>Whether a <c>git -c alias.x=…</c> value runs a push: its first word is <c>push</c>, or it is a shell alias (<c>!</c>) that has the word <c>push</c>.</summary>
    private static bool AliasRunsPush(string value)
    {
        string text = value.Trim();
        if (text.StartsWith('!'))
        {
            return PushWord().IsMatch(text);
        }

        int end = text.IndexOf(' ', StringComparison.Ordinal);
        return string.Equals(end < 0 ? text : text[..end], "push", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the positional words of <c>git config</c> set an alias whose value has the word <c>push</c> (<c>alias.p push</c>).</summary>
    private static bool SetsPushAlias(List<string> args)
    {
        int name = args.FindIndex(static arg => arg.StartsWith("alias.", StringComparison.OrdinalIgnoreCase));
        return name >= 0 && name + 1 < args.Count && PushWord().IsMatch(args[name + 1]);
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

    [GeneratedRegex(@"^alias\.(?<name>[^=]+)=(?<value>.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AliasDefinition();

    [GeneratedRegex(@"(?<![\w-])push(?![\w-])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PushWord();

    [GeneratedRegex(@"^\d*[<>]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RedirectionOnly();

    [GeneratedRegex(@"^\d*[<>]", RegexOptions.CultureInvariant)]
    private static partial Regex Redirection();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*=", RegexOptions.CultureInvariant)]
    private static partial Regex Assignment();
}
