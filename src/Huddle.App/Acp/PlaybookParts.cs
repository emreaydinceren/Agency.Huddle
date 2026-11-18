using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Acp;

/// <summary>Turns a <see cref="PlaybookSnapshot"/> into the labelled parts of the system prompt, applying the size limits (Team Playbooks spec §6.1 to §6.3).</summary>
internal static class PlaybookParts
{
    private const string FrameKey = "systemPrompt.playbooks";
    private const string CutKey = "systemPrompt.playbookCut";
    private const string CutNoFilesKey = "systemPrompt.playbookCutNoFiles";

    /// <summary>Adds the frame, the Huddle rules and the Playbooks; a <see langword="null"/> or empty snapshot adds nothing.</summary>
    /// <param name="parts">The prompt parts so far.</param>
    /// <param name="prompts">Resolves each prompt's current text.</param>
    /// <param name="playbooks">The snapshot, or <see langword="null"/>.</param>
    /// <param name="toolNames">The offered tools' names, already prefixed.</param>
    internal static void Add(List<PromptPart> parts, IPromptSource prompts, PlaybookSnapshot? playbooks, IReadOnlyList<string> toolNames)
    {
        if (playbooks is null || playbooks.Sections.Count == 0)
        {
            return;
        }

        List<PromptPart> block =
        [
            new("Playbooks", new PromptPartSource.Prompt(FrameKey, prompts.IsOverridden(FrameKey)), prompts.Render(FrameKey, new Dictionary<string, string>())),
            new("Huddle rules (Member)", new PromptPartSource.Embedded("huddleRules.member"), "## Huddle rules for members\n" + HuddleRules.RenderMember(toolNames)),
        ];
        List<Own> owns = [];
        AddSections(block, owns, prompts, playbooks, PlaybookKind.Member);
        if (playbooks.Sections.Any(static section => section.Kind == PlaybookKind.Manager))
        {
            block.Add(new PromptPart("Huddle rules (Manager)", new PromptPartSource.Embedded("huddleRules.manager"), "## Huddle rules for managers\n" + HuddleRules.RenderManager(toolNames)));
            AddSections(block, owns, prompts, playbooks, PlaybookKind.Manager);
        }

        DropOwnOverTotal(block, owns, playbooks.MaxTotalChars);
        parts.AddRange(block);
    }

    private static void AddSections(List<PromptPart> block, List<Own> owns, IPromptSource prompts, PlaybookSnapshot playbooks, PlaybookKind kind)
    {
        string label = kind == PlaybookKind.Manager ? "Manager Playbook" : "Member Playbook";
        foreach (PlaybookSection section in playbooks.Sections.Where(section => section.Kind == kind).OrderBy(static section => section.Source is PromptPartSource.File))
        {
            bool isOwn = section.Source is PromptPartSource.File;
            string names = JoinNames(section.Teams);
            string heading = isOwn ? $"## {label} for {names} (this Team's own)\n" : $"## {label} (Huddle default) for {names}\n";
            string pointer = Pointer(prompts, section.Source, playbooks.FilesReadable);
            string body = section.Text.Length > playbooks.MaxChars ? Cut(section.Text, playbooks.MaxChars) + "\n" + pointer : section.Text;
            block.Add(new PromptPart(label, section.Source, heading + body));
            if (isOwn)
            {
                owns.Add(new Own(block.Count - 1, label, section.Source, heading + pointer));
            }
        }
    }

    /// <summary>Replaces each Team's own Playbook by its pointer line, last first, until the block fits; the rules and the defaults stay.</summary>
    private static void DropOwnOverTotal(List<PromptPart> block, List<Own> owns, int maxTotalChars)
    {
        int total = block.Sum(static part => part.Text.Length);
        for (int index = owns.Count - 1; index >= 0 && total > maxTotalChars; index--)
        {
            Own own = owns[index];
            PromptPart dropped = new(own.Label, own.Source, own.Dropped);
            total += dropped.Text.Length - block[own.Index].Text.Length;
            block[own.Index] = dropped;
        }
    }

    private static string Cut(string text, int maxChars)
    {
        int lineBreak = text[..maxChars].LastIndexOf('\n');
        return text[..(lineBreak > 0 ? lineBreak : maxChars)];
    }

    private static string Pointer(IPromptSource prompts, PromptPartSource source, bool filesReadable) =>
        source is PromptPartSource.File file && filesReadable
            ? prompts.Render(CutKey, new Dictionary<string, string> { ["{{path}}"] = file.Path })
            : prompts.Render(CutNoFilesKey, new Dictionary<string, string>());

    private static string JoinNames(IReadOnlyList<string> teams) =>
        teams.Count <= 1 ? string.Concat(teams) : string.Join(", ", teams.Take(teams.Count - 1)) + " and " + teams[^1];

    private sealed record Own(int Index, string Label, PromptPartSource Source, string Dropped);
}
