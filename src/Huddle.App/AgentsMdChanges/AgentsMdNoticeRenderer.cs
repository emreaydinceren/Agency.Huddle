using System.Globalization;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// Turns the notices a Room has not heard into the text an Agent reads (AgentsMdChanges Spec §6.6). The total is capped in
/// order: each file keeps its last <c>MaxNoticesPerFile</c> revisions, then <c>MaxNoticeChars</c> bounds the revisions across
/// the block, oldest first. Whole-file text is never collapsed, only cut at <c>MaxCharsPerFile</c>.
/// </summary>
/// <param name="prompts">Resolves the <c>turn.agentsMd*</c> prompts.</param>
/// <param name="options">The caps.</param>
internal sealed class AgentsMdNoticeRenderer(IPromptSource prompts, AgentsMdOptions options)
{
    /// <summary>The number of changes in a Helper's summary that means the list may have been cut.</summary>
    private const int HelperChangeLimit = 12;

    /// <summary>Renders the whole block, or an empty string when there is nothing to tell.</summary>
    /// <param name="entries">One entry per file, in the order they are told.</param>
    /// <param name="fileTools">Whether the Adapter can read files; when not, the forms that carry the text are used.</param>
    /// <returns>The block, with its header once at the top.</returns>
    public string Render(IReadOnlyList<AgentsMdBlockEntry> entries, bool fileTools)
    {
        ArgumentNullException.ThrowIfNull(entries);

        List<List<Slot>> slots = [];
        foreach (AgentsMdBlockEntry entry in entries)
        {
            slots.Add(entry is AgentsMdBlockEntry.Changed changed ? this.SlotsOf(changed, fileTools) : []);
        }

        this.CollapseOverflow(slots);

        List<string> parts = [];
        for (int index = 0; index < entries.Count; index++)
        {
            string? part = this.PartOf(entries[index], slots[index], fileTools);
            if (part is not null)
            {
                parts.Add(part);
            }
        }

        return parts.Count == 0
            ? string.Empty
            : prompts.Render("turn.agentsMdHeader") + "\n\n" + string.Join('\n', parts);
    }

    /// <summary>Renders each told revision of a file, applying the per-file count.</summary>
    private List<Slot> SlotsOf(AgentsMdBlockEntry.Changed changed, bool fileTools)
    {
        List<Slot> slots = [];
        foreach (AgentsMdRevision revision in changed.Revisions)
        {
            string? text = this.RevisionText(changed, revision.Notice, fileTools);
            if (text is not null)
            {
                slots.Add(new Slot(revision.SettledUtc, text));
            }
        }

        int overflow = slots.Count - Math.Max(0, options.MaxNoticesPerFile);
        for (int index = 0; index < overflow; index++)
        {
            slots[index].Collapsed = true;
        }

        return slots;
    }

    /// <summary>Collapses the oldest revisions across the block until their text fits <c>MaxNoticeChars</c>.</summary>
    private void CollapseOverflow(List<List<Slot>> slots)
    {
        List<Slot> live = [.. slots.SelectMany(static list => list).Where(static slot => !slot.Collapsed)];
        int total = live.Sum(static slot => slot.Text.Length);
        foreach (Slot slot in live.OrderBy(static slot => slot.SettledUtc))
        {
            if (total <= options.MaxNoticeChars)
            {
                return;
            }

            slot.Collapsed = true;
            total -= slot.Text.Length;
        }
    }

    /// <summary>Renders one file's part of the block, or null when it has nothing to say.</summary>
    private string? PartOf(AgentsMdBlockEntry entry, List<Slot> slots, bool fileTools) => entry switch
    {
        AgentsMdBlockEntry.Changed changed => this.ChangedPart(changed, slots, fileTools),
        AgentsMdBlockEntry.NowFollow follow => this.WithText("turn.agentsMdNowFollow", follow.Text, ("path", follow.Path)),
        AgentsMdBlockEntry.NoLonger gone => prompts.Say("turn.agentsMdNoLonger", ("displayPath", gone.DisplayPath)),
        AgentsMdBlockEntry.Unknown unknown => this.UnknownText(unknown.DisplayPath, unknown.Path, unknown.Text, fileTools),
        _ => null,
    };

    /// <summary>The earlier-changes line, if any revision collapsed, then the revisions that stayed.</summary>
    private string? ChangedPart(AgentsMdBlockEntry.Changed changed, List<Slot> slots, bool fileTools)
    {
        List<string> lines = [];
        int collapsed = slots.Count(static slot => slot.Collapsed);
        if (collapsed > 0)
        {
            string count = collapsed.ToString(CultureInfo.InvariantCulture);
            lines.Add(fileTools
                ? prompts.Say("turn.agentsMdOlder", ("count", count), ("displayPath", changed.DisplayPath))
                : this.WithText("turn.agentsMdOlderNoFiles", changed.CurrentText, ("count", count), ("displayPath", changed.DisplayPath)));
        }

        lines.AddRange(slots.Where(static slot => !slot.Collapsed).Select(static slot => slot.Text));
        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    /// <summary>Renders one revision's notice, or null for one that is not told.</summary>
    private string? RevisionText(AgentsMdBlockEntry.Changed changed, AgentsMdNotice notice, bool fileTools) => notice switch
    {
        AgentsMdNotice.Summary summary => this.SummaryText(changed, summary, fileTools),
        AgentsMdNotice.Comparison comparison => this.ComparisonText(changed, comparison, fileTools),
        AgentsMdNotice.Unknown => this.UnknownText(changed.DisplayPath, changed.Path, changed.CurrentText, fileTools),
        _ => null,
    };

    /// <summary>A Helper's summary: the file line, each change, the 12-change warning, and the file-wins line.</summary>
    private string SummaryText(AgentsMdBlockEntry.Changed changed, AgentsMdNotice.Summary summary, bool fileTools)
    {
        List<string> lines = [prompts.Say("turn.agentsMdFile", ("displayPath", changed.DisplayPath))];
        foreach (AgentsMdRuleChange change in summary.Changes)
        {
            lines.Add(string.Equals(change.Kind, "removed", StringComparison.OrdinalIgnoreCase)
                ? prompts.Say("turn.agentsMdRemoved", ("summary", change.Summary), ("quote", change.Quote))
                : prompts.Say("turn.agentsMdChange", ("kind", KindLabel(change.Kind)), ("summary", change.Summary), ("quote", change.Quote)));
        }

        if (fileTools && summary.Changes.Count == HelperChangeLimit)
        {
            lines.Add(prompts.Render("turn.agentsMdMore"));
        }

        lines.Add(fileTools
            ? prompts.Say("turn.agentsMdFileWins", ("path", changed.Path))
            : prompts.Render("turn.agentsMdFileWinsNoFiles"));
        return string.Join('\n', lines);
    }

    /// <summary>A plain comparison inside a fence, then how many lines were left out.</summary>
    private string ComparisonText(AgentsMdBlockEntry.Changed changed, AgentsMdNotice.Comparison comparison, bool fileTools)
    {
        string body = string.Join('\n', comparison.Lines);
        string fence = ChooseFence(body);
        string text = fileTools
            ? prompts.Say("turn.agentsMdComparison", ("displayPath", changed.DisplayPath), ("path", changed.Path), ("fence", fence), ("lines", body))
            : prompts.Say("turn.agentsMdComparisonNoFiles", ("displayPath", changed.DisplayPath), ("fence", fence), ("lines", body));
        return comparison.NotShown > 0
            ? text + "\n" + prompts.Say("turn.agentsMdNotShown", ("count", comparison.NotShown.ToString(CultureInfo.InvariantCulture)))
            : text;
    }

    /// <summary>A file changed in an unknown way: a pointer to the file, or its text when there are no file tools.</summary>
    private string UnknownText(string displayPath, string path, string text, bool fileTools) => fileTools
        ? prompts.Say("turn.agentsMdUnknown", ("path", path))
        : this.WithText("turn.agentsMdUnknownNoFiles", text, ("displayPath", displayPath));

    /// <summary>Renders a prompt that carries a file's text, cut at <c>MaxCharsPerFile</c> and fenced.</summary>
    private string WithText(string key, string text, params (string Name, string Value)[] values)
    {
        int max = Math.Max(0, options.MaxCharsPerFile);
        string cut = text.Length > max ? text[..max] : text;
        return prompts.Say(key, [.. values, ("fence", ChooseFence(cut)), ("text", cut)]);
    }

    /// <summary>The label for a change kind: <c>Added</c> or <c>Changed</c>.</summary>
    private static string KindLabel(string kind) =>
        string.Equals(kind, "added", StringComparison.OrdinalIgnoreCase) ? "Added" : "Changed";

    /// <summary>The fence for fenced text: the longest run of tildes already in it, plus one, never shorter than three.</summary>
    private static string ChooseFence(string text)
    {
        int longest = 0;
        int current = 0;
        foreach (char c in text)
        {
            current = c == '~' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return new string('~', Math.Max(3, longest + 1));
    }

    /// <summary>One rendered revision, and whether the caps collapsed it.</summary>
    private sealed class Slot(DateTimeOffset settledUtc, string text)
    {
        public DateTimeOffset SettledUtc { get; } = settledUtc;

        public string Text { get; } = text;

        public bool Collapsed { get; set; }
    }
}
