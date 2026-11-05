namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>Who asks the ledger to start a judgement (AgentsMdChanges Spec §8.6).</summary>
internal enum JudgementReason
{
    /// <summary>The watcher: the file has been quiet, so the debounce applies.</summary>
    Debounced,

    /// <summary>A Turn needs the news, so the debounce is skipped.</summary>
    TurnAsked,
}
