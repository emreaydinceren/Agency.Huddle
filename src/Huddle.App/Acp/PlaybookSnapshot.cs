namespace Agency.Huddle.App.Acp;

/// <summary>The Playbooks one Agent's session starts with: pure data, built beside the AGENTS.md snapshot (Team Playbooks spec §6.1).</summary>
/// <param name="Sections">The sections: for each kind, the default first, then each Team's own in <c>teams</c> order. A Manager section exists only for a manager.</param>
internal sealed record PlaybookSnapshot(IReadOnlyList<PlaybookSection> Sections)
{
    /// <summary>An Agent in no Team: no Playbook part is composed.</summary>
    public static PlaybookSnapshot Empty { get; } = new([]);

    /// <summary>The most of one Playbook's text an Agent receives (<c>Team:Playbooks:MaxChars</c>).</summary>
    public int MaxChars { get; init; } = 4000;

    /// <summary>The most all Playbook parts may hold (<c>Team:Playbooks:MaxTotalChars</c>).</summary>
    public int MaxTotalChars { get; init; } = 16000;

    /// <summary>Whether the Adapter can read files, so a cut Playbook can point at its file.</summary>
    public bool FilesReadable { get; init; } = true;
}
