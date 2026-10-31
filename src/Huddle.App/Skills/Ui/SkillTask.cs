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
