namespace Agency.Huddle.App.Tasks;

/// <summary>The kind of one entry in a Task's timeline (Spec §7.7.1, ADR-0086).</summary>
public enum TimelineEntryKind
{
    /// <summary>A remark by a person or an Agent: Markdown, written by <c>add_comment</c> or the Task view's comment box.</summary>
    Comment,

    /// <summary>A comment tagged <c>evidence</c>: a command run, or a note about one, written by <c>add_evidence</c>.</summary>
    Evidence,

    /// <summary>One line of the Task's history (the old Change log), written only by <c>TaskService</c>.</summary>
    Event,
}
