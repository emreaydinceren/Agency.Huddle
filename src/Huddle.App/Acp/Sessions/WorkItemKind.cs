namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>What kind of Turn a <see cref="WorkItem"/> starts.</summary>
internal enum WorkItemKind
{
    /// <summary>An ordinary Turn, triggered by a delivered Message.</summary>
    Message,

    /// <summary>The Chief of Staff's unprompted first Message to the Human (Spec §6.14). No triggering Message and no catch-up.</summary>
    Greeting,

    /// <summary>
    /// An Adapter command a Human addressed to this Agent by Mention, for example <c>@Nova /compact</c>
    /// (Commands spec, section 6.5). The prompt is the bare command and nothing else, and the Turn
    /// neither collects File Changes nor drains Catch-up.
    /// </summary>
    Command,

    /// <summary>A goldfish possession's first Turn: the brief, framed by <c>turn.goldfishBrief</c> and nothing else.</summary>
    GoldfishBrief,

    /// <summary>A Message owed to the Agent while a goldfish possesses it: the sender and the text, framed by <c>turn.goldfishMessage</c> and nothing else.</summary>
    GoldfishMessage,
}
