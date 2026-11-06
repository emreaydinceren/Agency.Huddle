namespace Agency.Huddle.App.Elicitation;

/// <summary>A form an agent asked the Human to fill in, read from its JSON Schema.</summary>
/// <param name="Message">The prompt text the agent sent with the form, carried verbatim.</param>
/// <param name="Fields">The fields, in the schema's order.</param>
/// <param name="UntrustedDetail">
/// Text the host attached that came from the Agent's own request (a connector call's arguments), shown as plain text under a
/// line that says so, or <see langword="null"/>. Already stripped of control and bidi characters and capped by the host.
/// </param>
public sealed record ElicitationForm(string Message, IReadOnlyList<ElicitationField> Fields, string? UntrustedDetail = null)
{
    /// <summary>This form with the host's <see cref="UntrustedDetail"/> attached.</summary>
    /// <param name="detail">The detail, or <see langword="null"/> for none.</param>
    internal ElicitationForm WithDetail(string? detail) => this with { UntrustedDetail = detail };

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
