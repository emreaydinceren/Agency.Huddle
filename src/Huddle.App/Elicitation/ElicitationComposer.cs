using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// What the Human's answer to a form comes to: the text of the Transcript Message and the content the
/// wire carries back to the agent.
/// </summary>
/// <param name="Text">The Message text: each answered field's label quoted, a blank line, then the answer.</param>
/// <param name="Content">
/// The answers keyed by field, filled keys only, each a plain CLR value of the JSON type the server
/// expects: <see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/> or <c>string[]</c>.
/// </param>
internal sealed record ElicitationAnswer(string Text, IReadOnlyDictionary<string, object> Content);

/// <summary>
/// Turns what the Human entered into a form into the Transcript Message and the wire content, and says
/// whether the form may be sent at all (elicitation bridge, decisions E-5 and E-6). The entered values
/// are text, one list of entries per field: a scalar field holds zero or one entry, a multi select its
/// chosen option values. A blank entry is no entry. A typed custom answer beats its question's selection
/// exactly as the Adapter does: only the custom key is sent, trimmed, and the Transcript quotes it.
/// </summary>
internal static partial class ElicitationComposer
{
    /// <summary>
    /// Why <paramref name="values"/> may not be sent as an answer to <paramref name="form"/>, or
    /// <see langword="null"/> when they may: no value for a field the form lacks, every schema-required
    /// field set, at least one field filled when none is required, and every filled field valid for its
    /// kind (numbers parsed with the invariant culture inside their bounds, an integer whole, text inside
    /// its lengths, a date as <c>yyyy-MM-dd</c>, a select among its options).
    /// </summary>
    /// <param name="form">The form that was shown.</param>
    /// <param name="values">What the Human entered, by field key.</param>
    /// <returns>The first problem found, naming the field, or <see langword="null"/>.</returns>
    internal static string? Check(ElicitationForm form, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        foreach (string key in values.Keys.Order(StringComparer.Ordinal))
        {
            if (!form.Fields.Any(field => string.Equals(field.Key, key, StringComparison.Ordinal)))
            {
                return $"The form has no field '{key}'.";
            }
        }

        bool anyFilled = false;
        foreach (ElicitationField field in form.Fields)
        {
            List<string> entries = EntriesOf(values, field.Key);
            ElicitationField? companion = FindCompanion(form, field);
            if (companion is not null && EntriesOf(values, companion.Key).Count > 0)
            {
                // The typed answer stands in for this field's selection; its own turn in this loop checks it.
                anyFilled = true;
                continue;
            }

            if (entries.Count > 0)
            {
                string? invalid = CheckEntries(field, entries);
                if (invalid is not null)
                {
                    return invalid;
                }

                anyFilled = true;
            }
            else if (field.Required)
            {
                return $"Field '{field.Key}' is required.";
            }
        }

        return anyFilled ? null : "Fill in at least one field.";
    }

    /// <summary>
    /// Why the entries typed into one field may not be sent, or <see langword="null"/> when they may or
    /// there are none: <see cref="Check"/>'s own reason for that field, which is what the card shows beside
    /// it. A field left empty is the form's business (<c>required</c>, at least one filled), not the field's.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="entries">What the Human entered into it; blank entries are no entry.</param>
    /// <returns>The reason, naming the field, or <see langword="null"/>.</returns>
    internal static string? CheckField(ElicitationField field, IReadOnlyList<string> entries)
    {
        List<string> filled = [.. entries.Where(static entry => !string.IsNullOrWhiteSpace(entry))];
        return filled.Count == 0 ? null : CheckEntries(field, filled);
    }

    /// <summary>
    /// The allowed range of a number or an integer in plain words - <c>At least 1.</c>, <c>At most 20.</c>
    /// or <c>Between 1 and 20.</c> - naming only the bounds the schema declares, written with the
    /// invariant culture; <see langword="null"/> for any other field and for a number with no bounds.
    /// </summary>
    /// <param name="field">The field.</param>
    internal static string? RangeHint(ElicitationField field)
    {
        if (field.Kind is not (ElicitationFieldKind.Number or ElicitationFieldKind.Integer))
        {
            return null;
        }

        return (field.Minimum, field.Maximum) switch
        {
            ({ } minimum, { } maximum) => $"Between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.",
            ({ } minimum, null) => $"At least {minimum.ToString(CultureInfo.InvariantCulture)}.",
            (null, { } maximum) => $"At most {maximum.ToString(CultureInfo.InvariantCulture)}.",
            _ => null,
        };
    }

    /// <summary>Whether the Human may send <paramref name="values"/> as the answer to <paramref name="form"/>: <see cref="Check"/> found nothing wrong.</summary>
    /// <param name="form">The form that was shown.</param>
    /// <param name="values">What the Human entered, by field key.</param>
    internal static bool CanSend(ElicitationForm form, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        return Check(form, values) is null;
    }

    /// <summary>
    /// Composes the Transcript Message and the wire content from <paramref name="values"/>: fields in the
    /// form's order, unanswered ones omitted, a typed custom answer in place of its question's selection.
    /// </summary>
    /// <param name="form">The form that was shown.</param>
    /// <param name="values">What the Human entered, by field key; must pass <see cref="Check"/>.</param>
    /// <returns>The Message text and the wire content.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="values"/> do not pass <see cref="Check"/>; the form builds them, so this is a UI bug.</exception>
    internal static ElicitationAnswer Compose(ElicitationForm form, IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        string? problem = Check(form, values);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }

        Dictionary<string, object> content = new(StringComparer.Ordinal);
        List<string> blocks = [];
        foreach (ElicitationField field in form.Fields)
        {
            if (field.CustomAnswerFor is not null)
            {
                continue;
            }

            ElicitationField? companion = FindCompanion(form, field);
            List<string> custom = companion is null ? [] : EntriesOf(values, companion.Key);
            if (companion is not null && custom.Count > 0)
            {
                string typed = custom[0].Trim();
                content[companion.Key] = typed;
                blocks.Add(Block(form.NameOf(field), typed));
                continue;
            }

            List<string> entries = EntriesOf(values, field.Key);
            if (entries.Count == 0)
            {
                continue;
            }

            (object wire, string shown) = ToWire(field, entries);
            content[field.Key] = wire;
            blocks.Add(Block(form.NameOf(field), shown));
        }

        return new ElicitationAnswer(string.Join("\n\n", blocks), content);
    }

    /// <summary>One Transcript block: the label quoted on one line, a blank line, then the answer. The blank line keeps the answer out of the quote in CommonMark.</summary>
    /// <param name="label">The field's label; line breaks in it become spaces so model text cannot leave the quote.</param>
    /// <param name="answer">The answer as the Human gave it.</param>
    private static string Block(string label, string answer)
    {
        return $"> {LineBreakRegex().Replace(label, " ")}\n\n{answer}";
    }

    /// <summary>The wire value and the Transcript text of a filled field. The entries have passed <see cref="Check"/>.</summary>
    /// <param name="field">The field.</param>
    /// <param name="entries">Its non-blank entries.</param>
    private static (object Wire, string Shown) ToWire(ElicitationField field, List<string> entries)
    {
        switch (field.Kind)
        {
            case ElicitationFieldKind.Number:
                _ = TryParseNumber(entries[0], out double number);
                return (number, number.ToString(CultureInfo.InvariantCulture));
            case ElicitationFieldKind.Integer:
                _ = TryParseWhole(entries[0], out long whole);
                return (whole, whole.ToString(CultureInfo.InvariantCulture));
            case ElicitationFieldKind.Boolean:
                _ = bool.TryParse(entries[0], out bool flag);
                return (flag, flag ? "Yes" : "No");
            case ElicitationFieldKind.SingleSelect:
                ElicitationOption chosen = field.Options.First(option => string.Equals(option.Value, entries[0], StringComparison.Ordinal));
                return (chosen.Value, chosen.Label);
            case ElicitationFieldKind.MultiSelect:
                List<ElicitationOption> picked = [.. field.Options.Where(option => entries.Contains(option.Value, StringComparer.Ordinal))];
                return (picked.Select(option => option.Value).ToArray(), string.Join(", ", picked.Select(option => option.Label)));
            case ElicitationFieldKind.Date:
                _ = TryParseDate(entries[0], out DateOnly date);
                string formatted = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return (formatted, formatted);
            default:
                return (entries[0], entries[0]);
        }
    }

    /// <summary>Checks the filled <paramref name="entries"/> of one field against its kind; the first problem, or <see langword="null"/>.</summary>
    /// <param name="field">The field.</param>
    /// <param name="entries">Its non-blank entries.</param>
    private static string? CheckEntries(ElicitationField field, List<string> entries)
    {
        if (field.Kind != ElicitationFieldKind.MultiSelect && entries.Count > 1)
        {
            return $"Field '{field.Key}' takes one value.";
        }

        switch (field.Kind)
        {
            case ElicitationFieldKind.Number:
                return TryParseNumber(entries[0], out double number)
                    ? CheckBounds(field, number)
                    : $"Field '{field.Key}' must be a number.";
            case ElicitationFieldKind.Integer:
                return TryParseWhole(entries[0], out long whole)
                    ? CheckBounds(field, whole)
                    : $"Field '{field.Key}' must be a whole number.";
            case ElicitationFieldKind.Boolean:
                return bool.TryParse(entries[0], out _) ? null : $"Field '{field.Key}' must be true or false.";
            case ElicitationFieldKind.SingleSelect:
            case ElicitationFieldKind.MultiSelect:
                return entries.All(entry => field.Options.Any(option => string.Equals(option.Value, entry, StringComparison.Ordinal)))
                    ? null
                    : $"Field '{field.Key}' must be one of its options.";
            case ElicitationFieldKind.Date:
                return TryParseDate(entries[0], out _) ? null : $"Field '{field.Key}' must be a date in the form yyyy-MM-dd.";
            default:
                return CheckLength(field, field.CustomAnswerFor is null ? entries[0] : entries[0].Trim());
        }
    }

    /// <summary>Checks a parsed number against the field's bounds.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The parsed value.</param>
    private static string? CheckBounds(ElicitationField field, double value)
    {
        if (field.Minimum is { } minimum && value < minimum)
        {
            return $"Field '{field.Key}' must be at least {minimum.ToString(CultureInfo.InvariantCulture)}.";
        }

        if (field.Maximum is { } maximum && value > maximum)
        {
            return $"Field '{field.Key}' must be at most {maximum.ToString(CultureInfo.InvariantCulture)}.";
        }

        return null;
    }

    /// <summary>Checks a text against the field's length bounds.</summary>
    /// <param name="field">The field.</param>
    /// <param name="text">The text that would be sent.</param>
    private static string? CheckLength(ElicitationField field, string text)
    {
        if (field.MinLength is { } minimum && text.Length < minimum)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Field '{field.Key}' must be at least {minimum} characters.");
        }

        if (field.MaxLength is { } maximum && text.Length > maximum)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Field '{field.Key}' must be at most {maximum} characters.");
        }

        return null;
    }

    /// <summary>The field that is the typed "Other" answer of <paramref name="field"/>, or <see langword="null"/>.</summary>
    /// <param name="form">The form.</param>
    /// <param name="field">The question.</param>
    private static ElicitationField? FindCompanion(ElicitationForm form, ElicitationField field)
    {
        return form.Fields.FirstOrDefault(candidate => string.Equals(candidate.CustomAnswerFor, field.Key, StringComparison.Ordinal));
    }

    /// <summary>The non-blank entries entered for <paramref name="key"/>; empty when there are none.</summary>
    /// <param name="values">What the Human entered, by field key.</param>
    /// <param name="key">The field key.</param>
    private static List<string> EntriesOf(IReadOnlyDictionary<string, IReadOnlyList<string>> values, string key)
    {
        return values.TryGetValue(key, out IReadOnlyList<string>? entered)
            ? [.. entered.Where(entry => !string.IsNullOrWhiteSpace(entry))]
            : [];
    }

    /// <summary>Parses a number with the invariant culture, whatever the machine's own; NaN and infinity are not numbers here.</summary>
    /// <param name="text">The entered text.</param>
    /// <param name="number">The parsed value.</param>
    private static bool TryParseNumber(string text, out double number)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    /// <summary>Parses a whole number with the invariant culture: <c>3</c> and <c>3.0</c> are whole, <c>2.5</c> and anything beyond a <see cref="long"/> are not.</summary>
    /// <param name="text">The entered text.</param>
    /// <param name="whole">The parsed value.</param>
    private static bool TryParseWhole(string text, out long whole)
    {
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)
            && parsed == decimal.Truncate(parsed)
            && parsed >= long.MinValue
            && parsed <= long.MaxValue)
        {
            whole = (long)parsed;
            return true;
        }

        whole = 0;
        return false;
    }

    /// <summary>Parses a date written <c>yyyy-MM-dd</c>, and nothing else.</summary>
    /// <param name="text">The entered text.</param>
    /// <param name="date">The parsed date.</param>
    private static bool TryParseDate(string text, out DateOnly date)
    {
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Matches a run of line breaks.</summary>
    [GeneratedRegex(@"[\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakRegex();
}
