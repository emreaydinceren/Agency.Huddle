namespace Agency.Huddle.App.Acp;

/// <summary>
/// What the Reply Gate decided about one delivery.
/// </summary>
internal enum ReplyDecision
{
    /// <summary>
    /// Take a Turn. The caller takes and clears the Room's Catch-up buffer first, so the missed
    /// Messages ride along with this one.
    /// </summary>
    Reply,

    /// <summary>
    /// Do not take a Turn: the Message was not for this Agent. It is buffered as Catch-up and rides
    /// along the next time this Agent is Mentioned in that Room.
    /// </summary>
    CatchUp,

    /// <summary>
    /// Do not take a Turn: the Room has spent its Budget. The Message is <em>not</em> buffered — it
    /// is being held for re-delivery, and buffering it would show it to the Agent twice.
    /// </summary>
    BudgetExhausted,
}