using System.Globalization;
using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Makes <c>changes.md</c>, the Human's list of what one Memory Tidy pass did (Memory Tidy spec §6.13), from the pass's journal alone: the text of the list
/// is a function of the entries, the folders the live files are in and how a live file is linked, so it can be made again after a crash. The wording is
/// interface copy for the Human, not model-facing text, so it is code and not a prompt.
/// </summary>
internal static class MemoryTidyList
{
    /// <summary>The list's file name inside the pass folder.</summary>
    public const string FileName = "changes.md";

    /// <summary>The most removed tokens a rewrite's line names before it says <c>and N more</c>.</summary>
    internal const int RemovedTokenCap = 20;

    private const string AgentKeyPrefix = "agent:";
    private const string HuddleKey = "huddle";
    private const string DescribeKind = "describe";
    private const string MergeKind = "merge";
    private const string DropKind = "drop";

    /// <summary>The path of the list in a pass folder.</summary>
    /// <param name="passFolder">The pass folder.</param>
    /// <returns>The full path of <c>changes.md</c>.</returns>
    public static string PathIn(string passFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);

        return Path.Combine(passFolder, FileName);
    }

    /// <summary>Counts what a pass changed. Each applied step counts once as tidied or dropped, except a drop, which counts each file it removed.</summary>
    /// <param name="entries">The pass's journal.</param>
    /// <returns>The counts; the pass's Feed item is built from them.</returns>
    public static MemoryTidyCounts Count(IReadOnlyList<MemoryTidyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Dictionary<(string FolderKey, int Step), MemoryTidyEntry.StepStarted> started = StartedSteps(entries);
        int tidied = 0;
        int dropped = 0;
        HashSet<string> folders = new(StringComparer.Ordinal);
        foreach (MemoryTidyEntry.StepApplied applied in entries.OfType<MemoryTidyEntry.StepApplied>())
        {
            (int stepTidied, int stepDropped) = Weigh(applied, KindOf(applied.FolderKey, applied.Step, started));
            if (stepTidied + stepDropped > 0)
            {
                tidied += stepTidied;
                dropped += stepDropped;
                _ = folders.Add(applied.FolderKey);
            }
        }

        return new MemoryTidyCounts(tidied, dropped, folders.Count);
    }

    /// <summary>The text of <c>changes.md</c> for a journal.</summary>
    /// <param name="entries">The pass's journal.</param>
    /// <param name="keepDays">The age <c>reported.md</c> lines are dropped at, for the line that says so.</param>
    /// <param name="folderOf">The absolute path of a memory folder from its key (<c>agent:Nova</c>, <c>huddle</c>); <see langword="null"/> when it is not known.</param>
    /// <param name="hrefOf">
    /// The Library link of a live file from its absolute path, or <see langword="null"/> when it is under no Library root. The Library resolves a link
    /// only inside one root, and the live file is in another root than the list, so it is written as a link that names its own root, or as its absolute path
    /// in a code span when there is none.
    /// </param>
    /// <returns>The Markdown, ending with a line break.</returns>
    public static string Build(IReadOnlyList<MemoryTidyEntry> entries, int keepDays, Func<string, string?> folderOf, Func<string, string?> hrefOf)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(folderOf);
        ArgumentNullException.ThrowIfNull(hrefOf);

        MemoryTidyCounts counts = Count(entries);
        ListWriter writer = new(StartedSteps(entries), folderOf, hrefOf);
        StringBuilder text = new();
        string passId = entries.OfType<MemoryTidyEntry.PassStarted>().FirstOrDefault()?.PassId ?? string.Empty;
        _ = text.Append(passId.Length > 0 ? "# Memory Tidy, " + passId : "# Memory Tidy").Append("\n\n");
        if (EarlyEndLine(entries) is { } early)
        {
            _ = text.Append(early).Append("\n\n");
        }

        _ = text.Append(Summary(counts)).Append(" The files as they were are in this folder.\n")
            .Append("To undo a change, open the copy and put its text back. To keep a file out of every pass, add `tidy: keep` to its\n")
            .Append("frontmatter. Turn Memory Tidy off in Settings > Helpers.\n");
        foreach (Section section in writer.Sections(entries))
        {
            AppendSection(text, section);
        }

        AppendReported(text, entries, keepDays);
        return text.ToString();
    }

    /// <summary>Reads the pass's journal, makes the list and writes it to <c>changes.md</c> in the pass folder through a temporary file, so a reader sees the old list or the new one.</summary>
    /// <param name="passFolder">The pass folder, which holds the journal.</param>
    /// <param name="keepDays">The age <c>reported.md</c> lines are dropped at.</param>
    /// <param name="folderOf">The absolute path of a memory folder from its key.</param>
    /// <param name="hrefOf">The Library link of a live file from its absolute path.</param>
    /// <returns>The pass's counts, for its Feed item.</returns>
    public static MemoryTidyCounts Write(string passFolder, int keepDays, Func<string, string?> folderOf, Func<string, string?> hrefOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passFolder);

        IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(passFolder);
        AtomicFile.WriteText(PathIn(passFolder), Build(entries, keepDays, folderOf, hrefOf));
        return Count(entries);
    }

    /// <summary>The link a live file gets in the list: <c>?library=&lt;root&gt;/&lt;path&gt;</c>, the form the document viewer resolves across roots.</summary>
    /// <param name="resolver">Resolves an absolute path to its Library root and path.</param>
    /// <param name="absolutePath">The live file's absolute path.</param>
    /// <returns>The href, or <see langword="null"/> when the file is under no Library root.</returns>
    public static string? LibraryHref(LibraryPathResolver resolver, string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        return resolver.TryResolveAbsolute(absolutePath, out LibraryPath? path) ? MarkdownRenderer.BuildLibraryHref(path.Root.Id, path.RelativePath) : null;
    }

    private static Dictionary<(string FolderKey, int Step), MemoryTidyEntry.StepStarted> StartedSteps(IReadOnlyList<MemoryTidyEntry> entries)
    {
        Dictionary<(string FolderKey, int Step), MemoryTidyEntry.StepStarted> started = [];
        foreach (MemoryTidyEntry.StepStarted step in entries.OfType<MemoryTidyEntry.StepStarted>())
        {
            started[(step.FolderKey, step.Step)] = step;
        }

        return started;
    }

    private static string KindOf(string folderKey, int step, Dictionary<(string FolderKey, int Step), MemoryTidyEntry.StepStarted> started) =>
        started.TryGetValue((folderKey, step), out MemoryTidyEntry.StepStarted? found) ? found.Kind : string.Empty;

    private static bool IsKind(string kind, string expected) => string.Equals(kind, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>A describe step that wrote nothing kept the description it was shown.</summary>
    private static bool IsKept(MemoryTidyEntry.StepApplied applied, string kind) => IsKind(kind, DescribeKind) && applied.Written.Count == 0;

    private static (int Tidied, int Dropped) Weigh(MemoryTidyEntry.StepApplied applied, string kind)
    {
        if (IsKind(kind, DropKind))
        {
            return (0, Math.Max(1, applied.Removed.Count));
        }

        if (applied.CountsAsDrop)
        {
            return (0, 1);
        }

        return IsKept(applied, kind) ? (0, 0) : (1, 0);
    }

    private static string Summary(MemoryTidyCounts counts)
    {
        if (!counts.ChangedAnyFile)
        {
            return "No topic file was changed.";
        }

        string sentence = counts.Sentence();
        return string.Concat(char.ToUpperInvariant(sentence[0]).ToString(), sentence.AsSpan(1), ".");
    }

    private static string? EarlyEndLine(IReadOnlyList<MemoryTidyEntry> entries) =>
        entries.OfType<MemoryTidyEntry.PassEnded>().LastOrDefault()?.End switch
        {
            MemoryTidyPassEnd.Cap => "This pass stopped at the daily limit and goes on tomorrow.",
            MemoryTidyPassEnd.ThreeFailures => "This pass stopped after three failed calls.",
            MemoryTidyPassEnd.Stopped => "Huddle was closed during this pass; the rest waits for the next one.",
            _ => null,
        };

    private static void AppendSection(StringBuilder text, Section section)
    {
        _ = text.Append("\n## ").Append(HeadingOf(section.Key)).Append("\n\n");
        if (section.Lines.Count > 0)
        {
            _ = text.Append(string.Join('\n', section.Lines)).Append('\n');
        }

        if (section.NotTidied is { } notTidied)
        {
            _ = text.Append(section.Lines.Count > 0 ? "\n" : string.Empty).Append("Not tidied: ").Append(notTidied).Append("; it will be tried again when it changes.\n");
        }
    }

    private static void AppendReported(StringBuilder text, IReadOnlyList<MemoryTidyEntry> entries, int keepDays)
    {
        List<MemoryTidyEntry.LinesDropped> dropped = [.. entries.OfType<MemoryTidyEntry.LinesDropped>()];
        if (dropped.Count == 0)
        {
            return;
        }

        _ = text.Append("\n## reported.md\n\n");
        foreach (MemoryTidyEntry.LinesDropped lines in dropped)
        {
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"- {lines.Agent}: dropped {lines.Count} {(lines.Count == 1 ? "line" : "lines")} older than {keepDays} {(keepDays == 1 ? "day" : "days")}. Copy: {Wiki(lines.Copy)}");
            _ = text.Append(line).Append('\n');
        }
    }

    private static string HeadingOf(string folderKey)
    {
        if (string.Equals(folderKey, HuddleKey, StringComparison.Ordinal))
        {
            return "Huddle-wide Memory";
        }

        return folderKey.StartsWith(AgentKeyPrefix, StringComparison.Ordinal) ? folderKey[AgentKeyPrefix.Length..] : folderKey;
    }

    /// <summary>The path of a copy inside its pass folder, as <see cref="MemoryTidyBackups.FolderFor"/> places it: <c>Agents/Nova/x.md</c> or <c>Huddle/x.md</c>.</summary>
    private static string CopyPath(string folderKey, string file)
    {
        if (string.Equals(folderKey, HuddleKey, StringComparison.Ordinal))
        {
            return "Huddle/" + file;
        }

        return folderKey.StartsWith(AgentKeyPrefix, StringComparison.Ordinal) ? "Agents/" + folderKey[AgentKeyPrefix.Length..] + "/" + file : file;
    }

    /// <summary>A wikilink to a copy, by its path inside the pass folder: the nearest note of that path from the list wins, so it is this pass's copy.</summary>
    private static string Wiki(string copy) => "[[" + copy.Replace('\\', '/') + "]]";

    private static string Code(string text) => text.Contains('`', StringComparison.Ordinal) ? "`` " + text + " ``" : "`" + text + "`";

    private static string Codes(IEnumerable<string> names) => string.Join(", ", names.Select(Code));

    /// <summary>Words from a Helper or a check, on one line and without a closing full stop, so the list adds its own.</summary>
    private static string Plain(string text) => text.ReplaceLineEndings(" ").Trim().TrimEnd('.').TrimEnd();

    private static string ReasonClause(string reason) => Plain(reason).Length > 0 ? ": " + Plain(reason) + "." : ".";

    /// <summary>One folder's part of the list.</summary>
    /// <param name="key">The folder key.</param>
    private sealed class Section(string key)
    {
        public string Key => key;

        public List<string> Lines { get; } = [];

        public string? NotTidied { get; set; }
    }

    /// <summary>Turns the entries into the lines of the list; holds what the lines need from outside the journal.</summary>
    /// <param name="started">Each step's start entry, which holds its kind and reason.</param>
    /// <param name="folderOf">The absolute path of a memory folder from its key.</param>
    /// <param name="hrefOf">The Library link of a live file from its absolute path.</param>
    private sealed class ListWriter(
        Dictionary<(string FolderKey, int Step), MemoryTidyEntry.StepStarted> started,
        Func<string, string?> folderOf,
        Func<string, string?> hrefOf)
    {
        public List<Section> Sections(IReadOnlyList<MemoryTidyEntry> entries)
        {
            List<Section> sections = [];
            foreach (MemoryTidyEntry entry in entries)
            {
                switch (entry)
                {
                    case MemoryTidyEntry.StepApplied applied:
                        SectionOf(sections, applied.FolderKey).Lines.Add(this.AppliedLine(applied));
                        break;
                    case MemoryTidyEntry.StepRefused refused:
                        SectionOf(sections, refused.FolderKey).Lines.Add(RefusedLine(refused));
                        break;
                    case MemoryTidyEntry.StepSkipped skipped:
                        SectionOf(sections, skipped.FolderKey).Lines.Add("- **Skipped** " + Code(skipped.File) + ": changed while it was being tidied");
                        break;
                    case MemoryTidyEntry.StepFaulted faulted:
                        SectionOf(sections, faulted.FolderKey).Lines.Add("- **Not changed** " + Code(faulted.File) + ": the copy or the write failed");
                        break;
                    case MemoryTidyEntry.StepSettled settled:
                        if (this.SettledLine(settled) is { Length: > 0 } line)
                        {
                            SectionOf(sections, settled.FolderKey).Lines.Add(line);
                        }

                        break;
                    case MemoryTidyEntry.FolderNotJudged notJudged:
                        SectionOf(sections, notJudged.FolderKey).NotTidied = Plain(notJudged.Reason);
                        break;
                    default:
                        break;
                }
            }

            return sections;
        }

        private static Section SectionOf(List<Section> sections, string key)
        {
            Section? found = sections.Find(section => string.Equals(section.Key, key, StringComparison.Ordinal));
            if (found is null)
            {
                found = new Section(key);
                sections.Add(found);
            }

            return found;
        }

        private static string RefusedLine(MemoryTidyEntry.StepRefused refused) =>
            "- **Not changed** " + (refused.Files.Count > 0 ? Codes(refused.Files) : "the files it named") + ": " + Plain(refused.Refusal);

        private string AppliedLine(MemoryTidyEntry.StepApplied applied)
        {
            started.TryGetValue((applied.FolderKey, applied.Step), out MemoryTidyEntry.StepStarted? step);
            string kind = step?.Kind ?? string.Empty;
            string reason = step?.Reason ?? string.Empty;
            string[] named = [.. step?.Files.Select(static file => file.Name) ?? []];
            string fallback = named.Length > 0 ? named[0] : string.Empty;
            string target = applied.Written.Count > 0 ? applied.Written[0] : fallback;
            if (IsKept(applied, kind))
            {
                return "- **Kept the description** of " + Code(target) + ": " + Plain(applied.Note ?? reason);
            }

            if (IsKind(kind, DropKind))
            {
                string[] gone = applied.Removed.Count > 0 ? [.. applied.Removed] : named;
                return "- **Dropped** " + Codes(gone) + ReasonClause(reason) + JoinParts(" ", this.Parts(applied, withNow: false));
            }

            return HeadOf(kind, applied, named, target) + ReasonClause(reason) + RemovedClause(applied) + JoinParts("\n  ", this.Parts(applied, withNow: true));
        }

        private static string HeadOf(string kind, MemoryTidyEntry.StepApplied applied, string[] named, string target)
        {
            if (IsKind(kind, MergeKind))
            {
                return MergeHead(applied, named, target);
            }

            return IsKind(kind, DescribeKind) ? "- **New description** of " + Code(target) : "- **Shortened** " + Code(target);
        }

        private static string MergeHead(MemoryTidyEntry.StepApplied applied, string[] named, string target)
        {
            IEnumerable<string> sources = applied.Removed.Count > 0 ? applied.Removed : named.Where(name => !string.Equals(name, target, StringComparison.Ordinal));
            return "- **Merged** " + Codes(sources) + " into " + Code(target);
        }

        private static string RemovedClause(MemoryTidyEntry.StepApplied applied)
        {
            if (applied.RemovedTokens.Count == 0)
            {
                return string.Empty;
            }

            string tokens = string.Join(", ", applied.RemovedTokens.Take(RemovedTokenCap));
            int more = applied.RemovedTokens.Count - RemovedTokenCap;
            return " Removed: " + tokens + (more > 0 ? string.Create(CultureInfo.InvariantCulture, $" and {more} more") : string.Empty) + ".";
        }

        private static string JoinParts(string separator, List<string> parts) => parts.Count > 0 ? separator + string.Join(". ", parts) : string.Empty;

        private List<string> Parts(MemoryTidyEntry.StepApplied applied, bool withNow)
        {
            List<string> parts = [];
            if (withNow && applied.Written.Count > 0)
            {
                parts.Add("Now: " + string.Join(", ", applied.Written.Select(file => this.Live(applied.FolderKey, file))));
            }

            if (applied.Copies.Count > 0)
            {
                parts.Add((applied.Copies.Count == 1 ? "Copy: " : "Copies: ") + string.Join(", ", applied.Copies.Select(Wiki)));
            }

            if (applied.LeftBehind.Count > 0)
            {
                parts.Add("Left in place: " + Codes(applied.LeftBehind));
            }

            if (applied.Note is { Length: > 0 } note)
            {
                parts.Add(Plain(note));
            }

            return parts;
        }

        /// <summary>A live file: a link that names its Library root, or its absolute path in a code span when no root holds it.</summary>
        private string Live(string folderKey, string file)
        {
            string? folder = folderOf(folderKey);
            if (string.IsNullOrEmpty(folder))
            {
                return Code(file);
            }

            string absolute = Path.Combine(folder, file);
            return hrefOf(absolute) is { } href ? "[" + Code(file) + "](" + href + ")" : Code(absolute);
        }

        private string SettledLine(MemoryTidyEntry.StepSettled settled)
        {
            if (settled.NotDone)
            {
                started.TryGetValue((settled.FolderKey, settled.Step), out MemoryTidyEntry.StepStarted? step);
                string names = step is { Files.Count: > 0 } ? " (" + Codes(step.Files.Select(static file => file.Name)) + ")" : string.Empty;
                return "- Not done: the pass was stopped" + names;
            }

            return string.Join('\n', settled.ChangedFiles.Select(file => "- Changed while the pass was being stopped: " + Code(file) + ". "
                + (settled.CopiedFiles.Contains(file, StringComparer.Ordinal)
                    ? "Compare it with its copy: " + Wiki(CopyPath(settled.FolderKey, file))
                    : "It was changed by someone else; the pass did not touch it")));
        }
    }
}
