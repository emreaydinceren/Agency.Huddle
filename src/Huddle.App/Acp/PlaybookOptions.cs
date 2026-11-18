namespace Agency.Huddle.App.Acp;

/// <summary>Bound from <c>Team:Playbooks</c> (Team Playbooks spec §5.3): the size limits on the Playbooks an Agent receives.</summary>
public sealed class PlaybookOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Team:Playbooks";

    /// <summary>The most of one Playbook's text an Agent receives, in characters (<c>Team:Playbooks:MaxChars</c>); the rest is cut.</summary>
    public int MaxChars { get; set; } = 4000;

    /// <summary>The most all Playbook parts of one session may hold, in characters (<c>Team:Playbooks:MaxTotalChars</c>).</summary>
    public int MaxTotalChars { get; set; } = 16000;
}
