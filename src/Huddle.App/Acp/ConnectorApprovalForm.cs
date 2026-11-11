using System.Globalization;
using System.Text;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Builds and reads the form that asks the Human before a claude.ai connector tool runs (Agent Connectors spec 8.4): one required
/// choice with no default, <c>allow_once</c> or <c>refuse</c>. The tool name and the call's arguments came from the Agent's request
/// and may have come from an email it read, so they are hostile input: stripped of control, format and bidi characters, capped,
/// and shown as plain text.
/// </summary>
internal static class ConnectorApprovalForm
{
    /// <summary>The form field the Human answers.</summary>
    internal const string DecisionKey = "decision";

    /// <summary>The one value that lets the call run.</summary>
    internal const string AllowOnce = "allow_once";

    /// <summary>The value that refuses the call.</summary>
    internal const string Refuse = "refuse";

    /// <summary>The most characters of the call's arguments the card shows.</summary>
    internal const int MaxDetailLength = 4096;

    private const int MaxNameLength = 120;

    private const int MaxFieldNameLength = 80;

    private const int MaxValueLength = 1000;

    private static readonly JsonSerializerOptions SchemaOptions = new() { WriteIndented = false };

    /// <summary>Builds the form.</summary>
    /// <param name="agentName">The Agent that asks; blank reads as "An Agent".</param>
    /// <param name="context">The permission request.</param>
    /// <returns>The request to put to the Human.</returns>
    internal static ElicitationRequest Build(string agentName, PermissionRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string who = string.IsNullOrWhiteSpace(agentName) ? "An Agent" : Clean(agentName, MaxNameLength);
        string tool = Clean(context.ToolCall.ToolName ?? context.ToolCall.Title ?? string.Empty, MaxNameLength);
        ArgumentView arguments = ReadArguments(context.ToolCall.RawInputJson);
        string what = tool.Length == 0 ? "one of your connected accounts" : $"the connector tool {tool}";
        string message = arguments.Complete
            ? $"{who} wants to use {what}. Allow this one call?"
            : $"{who} wants to use {what}, but its arguments cannot be shown in full, so this call can only be refused.";

        // A call whose arguments are not all on the card cannot be allowed: the part left out could be the destination.
        object[] choices = arguments.Complete
            ? [new { @const = AllowOnce, title = "Allow once" }, new { @const = Refuse, title = "Refuse" }]
            : [new { @const = Refuse, title = "Refuse" }];
        object schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                [DecisionKey] = new { type = "string", title = arguments.Complete ? "Allow this call?" : "Your answer", oneOf = choices },
            },
            required = new[] { DecisionKey },
        };

        return new ElicitationRequest(
            context.SessionId,
            context.ToolCall.ToolCallId,
            message,
            JsonSerializer.Serialize(schema, SchemaOptions),
            arguments.Text);
    }

    /// <summary>Gets the tool name with control, format and bidi characters removed and at most <see cref="MaxNameLength"/> long, for the audit log.</summary>
    /// <param name="name">The tool name from the Adapter.</param>
    /// <returns>The cleaned name; <c>(none)</c> when blank.</returns>
    internal static string LoggableName(string? name)
    {
        string clean = Clean(name ?? string.Empty, MaxNameLength);
        return clean.Length == 0 ? "(none)" : clean;
    }

    /// <summary>Whether the Human accepted the form with <see cref="AllowOnce"/>.</summary>
    /// <param name="result">How the question ended.</param>
    /// <returns><see langword="true"/> only for an accepted, readable allow-once answer.</returns>
    internal static bool IsAllowOnce(ElicitationResult result) =>
        result is ElicitationAccepted accepted
        && accepted.Content.TryGetValue(DecisionKey, out object? value)
        && value is string text
        && string.Equals(text, AllowOnce, StringComparison.Ordinal);

    /// <summary>What the card shows of the call's arguments, and whether that is all of them.</summary>
    /// <param name="Text">The text under the "Agent's own words" line.</param>
    /// <param name="Complete">Whether every top-level field is on the card (a long text may be cut). When false only Refuse is offered.</param>
    private readonly record struct ArgumentView(string Text, bool Complete);

    /// <summary>
    /// Splits the arguments into their top-level fields, shortest first so a destination is never pushed out of view by a long body,
    /// each long text cut with a count. Anything that cannot be shown whole (not a JSON object, a nested value that would be cut, more
    /// fields than fit) is not complete, and the Human may only refuse it.
    /// </summary>
    private static ArgumentView ReadArguments(string? rawInput)
    {
        string raw = rawInput ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ArgumentView("(no arguments)", true);
        }

        List<Field> fields = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Unreadable(raw);
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                bool nested = property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
                string text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.GetRawText();
                string name = Clean(property.Name, MaxFieldNameLength);
                fields.Add(new Field(name.Length == 0 ? "(unnamed)" : name, Strip(text, keepNewlines: true), nested));
            }
        }
        catch (JsonException)
        {
            return Unreadable(raw);
        }

        return fields.Count == 0 ? new ArgumentView("(no arguments)", true) : Lay(fields);
    }

    private static ArgumentView Lay(List<Field> fields)
    {
        StringBuilder text = new();
        bool complete = true;
        int shown = 0;
        foreach (Field field in fields.OrderBy(static f => f.Value.Length))
        {
            string line = LineFor(field, out bool cut);
            if (text.Length + line.Length + 1 > MaxDetailLength)
            {
                break;
            }

            if (text.Length > 0)
            {
                text.Append('\n');
            }

            text.Append(line);
            shown++;

            // A cut string is only a long text. A cut object or array hides fields inside it, and one of them could be the destination.
            complete &= !(cut && field.Nested);
        }

        int hidden = fields.Count - shown;
        if (hidden > 0)
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n... {hidden} more fields not shown."));
        }

        return new ArgumentView(text.ToString(), complete && hidden == 0);
    }

    private static string LineFor(Field field, out bool cut)
    {
        string value = field.Value;
        cut = value.Length > MaxValueLength;
        string note = string.Empty;
        if (cut)
        {
            int keep = CutPoint(value, MaxValueLength);
            note = string.Create(CultureInfo.InvariantCulture, $"\n... {value.Length - keep} more characters not shown.");
            value = value[..keep];
        }

        // A line break inside a value must not pass for the start of another field.
        return $"{field.Name}: {value.Replace("\n", "\n    ", StringComparison.Ordinal)}{note.Replace("\n", "\n    ", StringComparison.Ordinal)}";
    }

    private static ArgumentView Unreadable(string raw)
    {
        string text = Strip(raw, keepNewlines: true);
        if (text.Length <= MaxDetailLength)
        {
            return new ArgumentView(text.Length == 0 ? "(no arguments)" : text, false);
        }

        int cut = CutPoint(text, MaxDetailLength);
        return new ArgumentView(string.Create(CultureInfo.InvariantCulture, $"{text[..cut]}\n... {text.Length - cut} more characters not shown."), false);
    }

    private static int CutPoint(string text, int max) => char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;

    private sealed record Field(string Name, string Value, bool Nested);

    private static string Clean(string text, int max)
    {
        string stripped = Strip(text, keepNewlines: false);
        return stripped.Length <= max ? stripped : stripped[..CutPoint(stripped, max)] + "...";
    }

    /// <summary>
    /// Drops control characters, format characters (bidi marks and overrides, zero-width, the tag characters outside the Basic
    /// Multilingual Plane) and line or paragraph separators. It reads whole code points, so a character written as a surrogate pair is judged as one.
    /// </summary>
    private static string Strip(string text, bool keepNewlines)
    {
        StringBuilder kept = new(text.Length);
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n' && keepNewlines)
            {
                kept.Append('\n');
                continue;
            }

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                continue;
            }

            kept.Append(rune);
        }

        return kept.ToString();
    }
}
