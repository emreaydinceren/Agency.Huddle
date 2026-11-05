namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>The JSON shape of one ledger file (Spec §7.1). <see cref="FirstSightHash"/> survives pruning of the chain.</summary>
internal sealed class AgentsMdLedgerDocument
{
    /// <summary>The file, relative to the Teams root, with <c>/</c> separators.</summary>
    public string? RelativePath { get; init; }

    /// <summary>The hash of the file's first sight; null only in a file of the wrong shape, which the ledger treats as corrupt.</summary>
    public string? FirstSightHash { get; init; }

    /// <summary>The settled head's hash; empty for an absent file, null only in a file of the wrong shape.</summary>
    public string? HeadHash { get; init; }

    /// <summary>The head's text; null when the body was over <c>MaxLedgerChars</c>.</summary>
    public string? HeadText { get; init; }

    /// <summary>The settled edits, oldest first.</summary>
    public List<RevisionDocument> Revisions { get; init; } = [];

    /// <summary>Maps a notice to its document.</summary>
    /// <param name="notice">The notice.</param>
    public static NoticeDocument FromNotice(AgentsMdNotice notice) => notice switch
    {
        AgentsMdNotice.Summary summary => new NoticeDocument
        {
            Kind = nameof(AgentsMdNotice.Summary),
            Changes = [.. summary.Changes.Select(static change => new ChangeDocument { Kind = change.Kind, Summary = change.Summary, Quote = change.Quote })],
        },
        AgentsMdNotice.Comparison comparison => new NoticeDocument { Kind = nameof(AgentsMdNotice.Comparison), Lines = [.. comparison.Lines], NotShown = comparison.NotShown },
        AgentsMdNotice.Skipped skipped => new NoticeDocument { Kind = nameof(AgentsMdNotice.Skipped), Reason = skipped.Reason },
        _ => new NoticeDocument { Kind = nameof(AgentsMdNotice.Unknown) },
    };

    /// <summary>Maps a document back to a notice; an unrecognised kind reads as <see cref="AgentsMdNotice.Unknown"/>.</summary>
    /// <param name="document">The document.</param>
    public static AgentsMdNotice ToNotice(NoticeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Kind switch
        {
            nameof(AgentsMdNotice.Summary) => new AgentsMdNotice.Summary([.. (document.Changes ?? []).Select(static change => new AgentsMdRuleChange(change.Kind, change.Summary, change.Quote))]),
            nameof(AgentsMdNotice.Comparison) => new AgentsMdNotice.Comparison([.. document.Lines ?? []], document.NotShown),
            nameof(AgentsMdNotice.Skipped) => new AgentsMdNotice.Skipped(document.Reason ?? string.Empty),
            _ => new AgentsMdNotice.Unknown(),
        };
    }

    /// <summary>One settled edit.</summary>
    internal sealed class RevisionDocument
    {
        /// <summary>The head before the edit.</summary>
        public string FromHash { get; init; } = string.Empty;

        /// <summary>The head after the edit.</summary>
        public string ToHash { get; init; } = string.Empty;

        /// <summary>When it settled.</summary>
        public DateTimeOffset SettledUtc { get; init; }

        /// <summary>What a Room is told.</summary>
        public NoticeDocument Notice { get; init; } = new();
    }

    /// <summary>A notice; the fields used depend on <see cref="Kind"/>.</summary>
    internal sealed class NoticeDocument
    {
        /// <summary><c>Summary</c>, <c>Comparison</c>, <c>Skipped</c> or <c>Unknown</c>.</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>The changes of a summary.</summary>
        public List<ChangeDocument>? Changes { get; init; }

        /// <summary>The lines of a comparison.</summary>
        public List<string>? Lines { get; init; }

        /// <summary>How many comparison lines were left out.</summary>
        public int NotShown { get; init; }

        /// <summary>Why an edit was skipped.</summary>
        public string? Reason { get; init; }
    }

    /// <summary>One changed rule.</summary>
    internal sealed class ChangeDocument
    {
        /// <summary>The kind of change.</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>The rule, in a sentence.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>The verified quote.</summary>
        public string Quote { get; init; } = string.Empty;
    }
}
