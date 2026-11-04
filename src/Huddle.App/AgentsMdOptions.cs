namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:AgentsMd</c>, per Spec §6.2 and §7.4: the size limits on the AGENTS.md files Agents receive.</summary>
public sealed class AgentsMdOptions
{
    /// <summary>The per-file cut, in characters (<c>Team:AgentsMd:MaxCharsPerFile</c>). Replaces <c>Team:Instructions:MaxChars</c>.</summary>
    public int MaxCharsPerFile { get; set; } = 8000;

    /// <summary>The budget for the whole AGENTS.md block, in characters (<c>Team:AgentsMd:MaxTotalChars</c>). Project files are dropped from the end first, then cut.</summary>
    public int MaxTotalChars { get; set; } = 32000;

    /// <summary>How long a Turn waits after the judgement of the current text started, in seconds (<c>Team:AgentsMd:NoticeWaitSeconds</c>). <c>0</c> never waits.</summary>
    public int NoticeWaitSeconds { get; set; } = 30;

    /// <summary>How long a file must stay unchanged before a judgement starts with no Turn asking, in seconds (<c>Team:AgentsMd:DebounceSeconds</c>).</summary>
    public int DebounceSeconds { get; set; } = 60;

    /// <summary>The least time between two judgements of one file, whoever asks, in seconds (<c>Team:AgentsMd:MinJudgeSeconds</c>).</summary>
    public int MinJudgeSeconds { get; set; } = 300;

    /// <summary>How often the watcher checks the AGENTS.md files, in seconds (<c>Team:AgentsMd:PollSeconds</c>).</summary>
    public int PollSeconds { get; set; } = 10;

    /// <summary>The largest edit, in characters, that a "not meaningful" Verdict may skip (<c>Team:AgentsMd:MaxSkipChangedChars</c>).</summary>
    public int MaxSkipChangedChars { get; set; } = 200;

    /// <summary>The number of revisions kept per file (<c>Team:AgentsMd:MaxRevisionsKept</c>).</summary>
    public int MaxRevisionsKept { get; set; } = 20;

    /// <summary>The number of revisions told to an Agent per file in one Turn (<c>Team:AgentsMd:MaxNoticesPerFile</c>).</summary>
    public int MaxNoticesPerFile { get; set; } = 5;

    /// <summary>The cap, in characters, on one Turn's change-notice block (<c>Team:AgentsMd:MaxNoticeChars</c>).</summary>
    public int MaxNoticeChars { get; set; } = 8000;

    /// <summary>The cap, in characters, on the changed lines a fallback comparison shows (<c>Team:AgentsMd:MaxComparisonChars</c>).</summary>
    public int MaxComparisonChars { get; set; } = 4000;

    /// <summary>The cap, in characters, on the comparison the change Helper sees (<c>Team:AgentsMd:MaxHelperComparisonChars</c>).</summary>
    public int MaxHelperComparisonChars { get; set; } = 12000;

    /// <summary>The largest new text, in characters, shown to the change Helper (<c>Team:AgentsMd:MaxHelperFileChars</c>).</summary>
    public int MaxHelperFileChars { get; set; } = 16000;

    /// <summary>The largest body, in characters, whose text the change ledger keeps (<c>Team:AgentsMd:MaxLedgerChars</c>).</summary>
    public int MaxLedgerChars { get; set; } = 256000;
}
