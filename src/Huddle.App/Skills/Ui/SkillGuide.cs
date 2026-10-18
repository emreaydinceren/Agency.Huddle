namespace Agency.Huddle.App.Skills.Ui;

/// <summary>What the prompt dialog asks the AI tool to do (Skill Packs spec §6.9.2). The first four are the radio choices of
/// <c>G5a</c>; <see cref="FetchSkill"/> and <see cref="NeedsSetup"/> copy at once without the dialog.</summary>
internal enum SkillTask
{
    /// <summary>Add from a link or folder.</summary>
    Add,

    /// <summary>Find Packs for a role.</summary>
    FindForRole,

    /// <summary>Check for updates.</summary>
    CheckForUpdates,

    /// <summary>Fix a problem.</summary>
    Fix,

    /// <summary>Fetch one Not downloaded Skill.</summary>
    FetchSkill,

    /// <summary>Set up what a draft needs.</summary>
    NeedsSetup,
}

/// <summary>The guidance text of Settings › Skills (Skill Packs spec §6.9.12): the three tab intros and the one sentence each
/// button and menu item carries. UI text, not model-facing text (ADR-0007).</summary>
internal static class SkillGuide
{
    /// <summary>The Skills tab's intro, before <b>Learn more</b>.</summary>
    public const string SkillsIntro =
        "A Skill is a folder of instructions that a Teammate reads when its work calls for it. "
        + "This list shows every Skill Huddle knows about: built in, fetched by your AI tool, or listed in a catalog but not on this computer yet. "
        + "A Skill marked Not downloaded has no folder here; Copy prompt gives your AI tool what it needs to fetch it. "
        + "Huddle never downloads Skills itself. "
        + "Giving a Teammate a Skill copies it to that Teammate, so later changes reach it only when you apply an update.";

    /// <summary>The Packs tab's intro, before <b>Learn more</b>.</summary>
    public const string PacksIntro =
        "A Pack is a named set of Skills for some roles, such as MSBuild Authoring. "
        + "Choosing a Pack on a Teammate gives it all the Pack's Skills at once and tells its manager when to use them. "
        + "Packs come from SKILLS.md, a list your AI tool keeps; Huddle never fetches one itself. "
        + "To add one, select Add with your AI tool, say where it is, and paste the prompt into Claude Code, Codex or a similar tool. "
        + "Whatever passes Huddle's checks is ready to use at once; there is no approval step.";

    /// <summary>The Sources tab's intro, before <b>Learn more</b>.</summary>
    public const string SourcesIntro =
        "A Source is one place Skills come from: a GitHub repository, another Git server or a folder on this computer. "
        + "Your AI tool keeps notes for each Source on how it found and fetched it, so the next update starts from what it learned. "
        + "Everything here lives in the Skill Sources folder, which you can share with another Huddle. "
        + "Removing a Source removes its notes and rows, but never the clone itself. "
        + "Your AI tool reads the install file first; Copy path copies its location.";

    /// <summary>The one sentence for each button and menu item, by its label, shown as its tooltip and, in a dialog, as a
    /// caption under the row.</summary>
    public static IReadOnlyDictionary<string, string> ButtonSentences { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Add with your AI tool"] = "Opens a prompt to paste into Claude Code, Codex or another AI tool, which fetches the Skills and adds them to SKILLS.md.",
        ["Check for updates"] = "Opens a prompt that asks your AI tool to look for newer versions of the Sources you pick. Huddle itself never goes online.",
        ["Reload"] = "Reads SKILLS.md and every file it points to again, right now, instead of waiting for a change.",
        ["Import skill"] = "Opens a prompt that asks your AI tool to fetch a single Skill from a link or folder.",
        ["Copy prompt"] = "Copies a prompt that asks your AI tool to fetch this one Skill. Paste it into your AI tool.",
        ["Copy fix prompt"] = "Copies a prompt that names the row and the checks it failed, for your AI tool to repair.",
        ["Copy path"] = "Copies the location of InstallSkillPack.md, in the form your AI tool can open.",
        ["Path forms"] = "E:\\…, /mnt/e/… or E:/…: pick the one your AI tool runs under. Huddle remembers the choice until you reload the page.",
        ["Details"] = "Shows where this came from, its Skills, their licences and warnings, and the notes your AI tool kept.",
        ["Review update"] = "Shows what changed and which Teammates are affected, and lets you apply it.",
        ["Hide"] = "Leaves it out of lists on this Huddle and any that shares the folder. Nothing is deleted and Teammates who have it keep it.",
        ["Unhide"] = "Brings it back into lists on this Huddle and any that shares the folder.",
        ["Show hidden"] = "Includes what you hid, so you can unhide it.",
        ["Show in Library"] = "Opens the pack or catalog file, read only, in the Library.",
        ["Show notes"] = "Opens the notes your AI tool kept for this Source, in the Library.",
        ["Remove Pack"] = "Removes its row from SKILLS.md and its file if Huddle's folder holds it. Your AI tool's clone stays.",
        ["Remove from Teammates"] = "Takes this Pack off every Teammate that has it. Their copies of the Skills stay.",
        ["Remove catalog"] = "Removes the catalog's row, and its file if Huddle's folder holds it. Teammates keep their copies.",
        ["Remove Source"] = "Deletes this Source's notes, its rows and the files they point to inside Huddle's folder. The clone stays.",
        ["Change"] = "Chooses another Skill Sources folder, for example a synced one shared with another Huddle. It does not move files.",
        ["Use default"] = "Goes back to the folder from configuration.",
        ["Learn more"] = "Opens a help page in Huddle about this tab; it takes you nowhere else.",
        ["Update"] = "Applies the update: copies the changed Skill files, then changes the Teammates you ticked.",
        ["Not now"] = "Closes without changing anything.",
        ["Keep or remove Skills"] = "Only stops tracking the Pack, or also takes off the Skills nobody else gave them.",
        ["Restore default"] = "Deletes the edited copy of a built-in Skill so the shipped one is used again.",
    };

    /// <summary>The help views, in tab order, with the dialog title of each.</summary>
    public static IReadOnlyList<(string View, string Title)> HelpViews { get; } = [("skills", "Skills"), ("packs", "Packs"), ("sources", "Sources")];

    /// <summary>The Markdown <c>SkillsHelpDialog</c> shows for a tab (§6.9.12): the body of the matching subsection of the
    /// User Guide, word for word, so the two cannot drift (<c>SkillsHelpTextTests</c> checks it). An unknown view is Skills.</summary>
    public static string HelpFor(string? view)
    {
        return view?.ToLowerInvariant() switch
        {
            "packs" => $"""
                {PacksIntro}

                Each card shows the Pack's title, version and state, its Source, how many Skills it holds, when it was last updated and the roles it is for. A line under the title says what is wrong when something is. **Details** shows where the Pack came from, each Skill with its licence and warnings, and the notes your AI tool kept.

                A card marked **New** appeared since you last left this tab. **Check for updates** opens a prompt that asks your AI tool to look for newer versions. **Hide** leaves a Pack out of lists; Teammates who have it keep it.
                """,
            "sources" => $"""
                {SourcesIntro}

                The tab shows the install file and this computer's name, the Skill Sources folder with **Change…** to choose another, one row per Source and one row per catalog. A Source its owner deprecated, withdrew or moved says so on its row, in words. Withdrawn Skills cannot be given to new hires; Teammates who already have them keep working and are flagged.
                """,
            _ => $"""
                {SkillsIntro}

                Each row shows the Skill's name and description, where it came from, its state and the Teammates that use it. Search by name, narrow the list with the **State** filter, or turn on **Show hidden** to see what you hid. The **⋯** menu on a row offers **Copy prompt**, **Hide** or **Unhide**, **Show catalog in Library** and, for a built-in Skill you changed, **Restore default**.

                **Reload** reads SKILLS.md and every file it points to again, right now. **Problems** at the bottom lists what failed a check, with the file and the fix.
                """,
        };
    }

    /// <summary>The dialog title of a help view.</summary>
    public static string HelpTitle(string? view)
    {
        return HelpViews.FirstOrDefault(entry => string.Equals(entry.View, view, StringComparison.OrdinalIgnoreCase)).Title ?? "Skills";
    }

    /// <summary>The sentence for a control label.</summary>
    /// <exception cref="KeyNotFoundException">The label has no sentence; every control must have one.</exception>
    public static string For(string control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return ButtonSentences.TryGetValue(control, out string? sentence)
            ? sentence
            : throw new KeyNotFoundException($"The control '{control}' has no guidance sentence.");
    }
}
