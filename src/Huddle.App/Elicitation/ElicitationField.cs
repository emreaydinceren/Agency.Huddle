namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// One field of a form. Every text in it comes from an agent or an MCP server and is untrusted: it is
/// carried verbatim, and the Room renders it as plain text, never Markdown.
/// </summary>
/// <param name="Key">The property name, the key the answer goes back under.</param>
/// <param name="Label">What the field is called, and what the Transcript quotes: the title, or the key when there is none. For an AskUserQuestion question it is the question itself.</param>
/// <param name="Description">The schema's secondary text, or <see langword="null"/>; none for an AskUserQuestion question, whose description became its <paramref name="Label"/>.</param>
/// <param name="Kind">What the field asks for.</param>
/// <param name="Options">The choices of a select; empty for every other kind.</param>
/// <param name="Required">Whether the schema's top-level <c>required</c> list names this field.</param>
/// <param name="Minimum">The lower bound of a number or integer, when the schema gives one.</param>
/// <param name="Maximum">The upper bound of a number or integer, when the schema gives one.</param>
/// <param name="MinLength">The shortest a text may be, when the schema gives one.</param>
/// <param name="MaxLength">The longest a text may be, when the schema gives one.</param>
/// <param name="CustomAnswerFor">
/// The key of the question this field is the typed "Other" answer of (AskUserQuestion's
/// <c>question_n_custom</c>), or <see langword="null"/> for an ordinary field. Typing in it beats the
/// question's selection, exactly as the Adapter does.
/// </param>
/// <param name="Header">The short heading an AskUserQuestion question carries (its title), or <see langword="null"/>.</param>
/// <param name="LabelIsKey">Whether <paramref name="Label"/> is only the property name, because the schema gave the field no title and no description of its own to call it by.</param>
public sealed record ElicitationField(
    string Key,
    string Label,
    string? Description,
    ElicitationFieldKind Kind,
    IReadOnlyList<ElicitationOption> Options,
    bool Required,
    double? Minimum,
    double? Maximum,
    int? MinLength,
    int? MaxLength,
    string? CustomAnswerFor,
    string? Header = null,
    bool LabelIsKey = false);
