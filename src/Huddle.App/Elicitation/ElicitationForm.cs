using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// What a field of an agent's form asks the Human for (elicitation bridge, decision E-6). Each kind is
/// one of the JSON Schema shapes the bridge can show faithfully; anything else is refused whole by
/// <c>ElicitationSchemaReader</c>, because a number or a boolean degraded to a text box would be sent
/// as a string and rejected by the server.
/// </summary>
public enum ElicitationFieldKind
{
    /// <summary>Free text: a <c>string</c> with no options, whatever its format except <c>date</c>.</summary>
    Text,

    /// <summary>A <c>number</c>, sent as a <see cref="double"/>.</summary>
    Number,

    /// <summary>An <c>integer</c>, sent as a <see cref="long"/>.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Integer is the JSON Schema type name this kind is read from, and what a reader of the schema expects it to be called; it names no CLR type here.")]
    Integer,

    /// <summary>A <c>boolean</c>, shown as Yes or No.</summary>
    Boolean,

    /// <summary>Exactly one of a fixed list of options: a <c>string</c> with <c>enum</c> or <c>oneOf</c>.</summary>
    SingleSelect,

    /// <summary>Any number of a fixed list of options: an <c>array</c> of string <c>enum</c> or <c>anyOf</c>.</summary>
    MultiSelect,

    /// <summary>A calendar date: a <c>string</c> with <c>format: date</c>, sent as <c>yyyy-MM-dd</c>.</summary>
    Date,
}

/// <summary>One choice of a select field. All three texts are the agent's own, carried verbatim.</summary>
/// <param name="Value">What goes on the wire when it is chosen: the schema's <c>const</c> (or the enum string).</param>
/// <param name="Label">What the Human reads: the option's <c>title</c>, or <paramref name="Value"/> when it has none.</param>
/// <param name="Description">The option's secondary text, or <see langword="null"/>.</param>
public sealed record ElicitationOption(string Value, string Label, string? Description);

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

/// <summary>A form an agent asked the Human to fill in, read from its JSON Schema.</summary>
/// <param name="Message">The prompt text the agent sent with the form, carried verbatim.</param>
/// <param name="Fields">The fields, in the schema's order.</param>
public sealed record ElicitationForm(string Message, IReadOnlyList<ElicitationField> Fields)
{
    /// <summary>
    /// The one thing the form asks, or <see langword="null"/> when it asks several: the only field that is
    /// not the typed "Other" answer of another, so a question with its companion counts as one.
    /// </summary>
    public ElicitationField? SoleField
    {
        get
        {
            List<ElicitationField> asked = [.. this.Fields.Where(static candidate => candidate.CustomAnswerFor is null)];
            return asked.Count == 1 ? asked[0] : null;
        }
    }

    /// <summary>
    /// Whether the message only says again what the form's one field is called: there is exactly one
    /// field, its label is a real one (not the bare key) and it reads the same as the message once both
    /// are trimmed, compared ordinally. The card then shows the sentence once.
    /// </summary>
    public bool MessageRepeatsLabel => this.SoleField is { LabelIsKey: false } sole
        && string.Equals(this.Message.Trim(), sole.Label.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// Whether the message is the only name the form's one field has: there is exactly one field, its
    /// label is only its key, and the message has text. The key is then never shown to the Human, and
    /// the message names the field instead.
    /// </summary>
    public bool MessageStandsInForLabel => this.SoleField is { LabelIsKey: true } && !string.IsNullOrWhiteSpace(this.Message);

    /// <summary>
    /// What <paramref name="field"/> is called wherever it is named - the Transcript quote and the accessible
    /// name of its group: its label, or the trimmed message for the one key-labelled field of a one-field form.
    /// </summary>
    /// <param name="field">One of this form's fields.</param>
    public string NameOf(ElicitationField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        return this.MessageStandsInForLabel && string.Equals(this.SoleField?.Key, field.Key, StringComparison.Ordinal)
            ? this.Message.Trim()
            : field.Label;
    }
}
