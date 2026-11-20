namespace Agency.Huddle.App.Components.Settings;

/// <summary>Which prompts the Prompts tab's list shows: every one, or only those whose text differs from what is saved.</summary>
public enum PromptFilter
{
    /// <summary>Every prompt.</summary>
    All,

    /// <summary>Only a prompt edited in this session and not yet saved.</summary>
    Changed,
}
