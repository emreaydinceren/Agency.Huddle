using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// Reads the JSON Schema of an agent's form into an <see cref="ElicitationForm"/> (elicitation bridge,
/// decision E-6). The schema is untrusted input - an agent, or an MCP server behind it, wrote it - so the
/// reader accepts a fixed set of shapes and refuses everything else whole, with a reason that names the
/// offending property: <c>string</c> (plain, <c>enum</c>, <c>oneOf</c>, <c>format: date</c>),
/// <c>integer</c>, <c>number</c>, <c>boolean</c>, and an <c>array</c> of string <c>enum</c> or
/// <c>anyOf</c>. A shape it cannot show is never degraded to a text box, because the server would reject
/// the string. Every text is carried verbatim; the Room renders it as plain text.
/// </summary>
internal static partial class ElicitationSchemaReader
{
    /// <summary>The most properties one form may have; a longer form is refused.</summary>
    internal const int MaxProperties = 20;

    /// <summary>The most options one field may have; a longer list is refused.</summary>
    internal const int MaxOptions = 50;

    private const string NotJsonProblem = "The form is not valid JSON.";

    /// <summary>
    /// Reads <paramref name="request"/>'s schema. On success <paramref name="form"/> is the typed form
    /// and <paramref name="problem"/> is <see langword="null"/>; otherwise <paramref name="form"/> is
    /// <see langword="null"/> and <paramref name="problem"/> says why the bridge must decline.
    /// </summary>
    /// <param name="request">The agent's request: its prompt text and its schema as JSON text.</param>
    /// <param name="form">The typed form, when the schema is supported.</param>
    /// <param name="problem">Why the schema is not supported, or <see langword="null"/> when it is.</param>
    /// <returns><see langword="true"/> when the schema is a form the bridge can show.</returns>
    internal static bool TryRead(ElicitationRequest request, [NotNullWhen(true)] out ElicitationForm? form, out string? problem)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(request.RequestedSchemaJson);
            form = ReadForm(document.RootElement, request.Message, out problem);
            return form is not null;
        }
        catch (JsonException)
        {
            // Untrusted input: text that is not JSON is refused like any other shape we cannot show.
            form = null;
            problem = NotJsonProblem;
            return false;
        }
    }

    /// <summary>Reads the top-level object: its type, its keywords, its property count and its <c>required</c> list.</summary>
    /// <param name="root">The schema's top-level value.</param>
    /// <param name="message">The request's prompt text.</param>
    /// <param name="problem">Why the form is refused, or <see langword="null"/> when it is read.</param>
    private static ElicitationForm? ReadForm(JsonElement root, string message, out string? problem)
    {
        if (root.ValueKind != JsonValueKind.Object || (root.TryGetProperty("type", out JsonElement type) && !IsString(type, "object")))
        {
            problem = "The form's top level must be an object.";
            return null;
        }

        if (UnsupportedKeyword(root) is { } keyword)
        {
            problem = $"The form uses '{keyword}', which is not supported.";
            return null;
        }

        if (!root.TryGetProperty("properties", out JsonElement properties) || properties.ValueKind != JsonValueKind.Object)
        {
            problem = "The form has no properties.";
            return null;
        }

        List<JsonProperty> entries = [.. properties.EnumerateObject()];
        if (entries.Count == 0)
        {
            problem = "The form has no properties.";
            return null;
        }

        if (entries.Count > MaxProperties)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"The form has {entries.Count} properties; the limit is {MaxProperties}.");
            return null;
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (JsonProperty entry in entries)
        {
            if (!keys.Add(entry.Name))
            {
                problem = $"Property '{entry.Name}' appears more than once.";
                return null;
            }
        }

        FormContext context = new(message, keys, RequiredKeys(root), entries.Count(entry => QuestionKeyRegex().IsMatch(entry.Name)));
        List<ElicitationField> fields = [];
        foreach (JsonProperty entry in entries)
        {
            ElicitationField? field = ReadField(entry.Name, entry.Value, context, out problem);
            if (field is null)
            {
                return null;
            }

            fields.Add(field);
        }

        problem = null;
        return new ElicitationForm(message, UntangleCustomFields(fields));
    }

    /// <summary>Reads one property into a field, or says why it is not a supported shape.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="node">The property's schema.</param>
    /// <param name="context">What the whole form says about this property.</param>
    /// <param name="problem">Why the property is refused, or <see langword="null"/> when it is read.</param>
    private static ElicitationField? ReadField(string key, JsonElement node, FormContext context, out string? problem)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            problem = $"Property '{key}' is not an object.";
            return null;
        }

        if (UnsupportedKeyword(node) is { } keyword)
        {
            problem = $"Property '{key}' uses '{keyword}', which is not supported.";
            return null;
        }

        if (!node.TryGetProperty("type", out JsonElement typeNode))
        {
            problem = $"Property '{key}' has no type.";
            return null;
        }

        if (typeNode.ValueKind != JsonValueKind.String)
        {
            problem = $"Property '{key}' has a type that is not a single name.";
            return null;
        }

        string type = typeNode.GetString() ?? string.Empty;
        Shape? shape = type switch
        {
            "string" => ReadString(key, node, out problem),
            "integer" => ReadNumber(key, node, ElicitationFieldKind.Integer, out problem),
            "number" => ReadNumber(key, node, ElicitationFieldKind.Number, out problem),
            "boolean" => ReadBoolean(out problem),
            "array" => ReadArray(key, node, out problem),
            _ => ReadUnsupported(key, type, out problem),
        };
        if (shape is null)
        {
            return null;
        }

        return BuildField(key, node, shape, context);
    }

    /// <summary>Reads a <c>string</c>: a select when it has <c>oneOf</c> or <c>enum</c>, a date for <c>format: date</c>, text otherwise.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="node">The property's schema.</param>
    /// <param name="problem">Why the property is refused, or <see langword="null"/>.</param>
    private static Shape? ReadString(string key, JsonElement node, out string? problem)
    {
        if (node.TryGetProperty("anyOf", out _))
        {
            problem = $"Property '{key}' uses 'anyOf', which is not supported.";
            return null;
        }

        if (node.TryGetProperty("oneOf", out JsonElement oneOf))
        {
            return ReadConstOptions(key, oneOf, ElicitationFieldKind.SingleSelect, out problem);
        }

        if (node.TryGetProperty("enum", out JsonElement names))
        {
            return ReadEnumOptions(key, names, ElicitationFieldKind.SingleSelect, out problem);
        }

        problem = null;
        ElicitationFieldKind kind = TryGetString(node, "format") == "date" ? ElicitationFieldKind.Date : ElicitationFieldKind.Text;
        return new Shape(kind, [], null, null, TryGetInt(node, "minLength"), TryGetInt(node, "maxLength"));
    }

    /// <summary>Reads an <c>integer</c> or <c>number</c>; one with a fixed set of values is refused.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="node">The property's schema.</param>
    /// <param name="kind">Integer or Number.</param>
    /// <param name="problem">Why the property is refused, or <see langword="null"/>.</param>
    private static Shape? ReadNumber(string key, JsonElement node, ElicitationFieldKind kind, out string? problem)
    {
        if (node.TryGetProperty("enum", out _) || node.TryGetProperty("oneOf", out _) || node.TryGetProperty("anyOf", out _))
        {
            problem = $"Property '{key}' is a number with a fixed set of values, which is not supported.";
            return null;
        }

        problem = null;
        return new Shape(kind, [], TryGetDouble(node, "minimum"), TryGetDouble(node, "maximum"), null, null);
    }

    /// <summary>Reads a <c>boolean</c>.</summary>
    /// <param name="problem">Always <see langword="null"/>: a boolean has no unsupported form.</param>
    private static Shape ReadBoolean(out string? problem)
    {
        problem = null;
        return new Shape(ElicitationFieldKind.Boolean, [], null, null, null, null);
    }

    /// <summary>Reads an <c>array</c>: a multi select when its items are a string <c>enum</c> or an <c>anyOf</c> of constants.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="node">The property's schema.</param>
    /// <param name="problem">Why the property is refused, or <see langword="null"/>.</param>
    private static Shape? ReadArray(string key, JsonElement node, out string? problem)
    {
        if (!node.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Object)
        {
            problem = $"Property '{key}' is an array whose items are not a list of strings.";
            return null;
        }

        if (TryGetString(items, "type") == "object")
        {
            problem = $"Property '{key}' is an array of objects, which is not supported.";
            return null;
        }

        if (items.TryGetProperty("anyOf", out JsonElement anyOf))
        {
            return ReadConstOptions(key, anyOf, ElicitationFieldKind.MultiSelect, out problem);
        }

        if (items.TryGetProperty("enum", out JsonElement names))
        {
            return ReadEnumOptions(key, names, ElicitationFieldKind.MultiSelect, out problem);
        }

        problem = $"Property '{key}' is an array whose items are not a list of strings.";
        return null;
    }

    /// <summary>Refuses a type the bridge does not show: an object, null, anything unknown.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="type">The type name.</param>
    /// <param name="problem">The reason, always set.</param>
    private static Shape? ReadUnsupported(string key, string type, out string? problem)
    {
        problem = $"Property '{key}' has the unsupported type '{type}'.";
        return null;
    }

    /// <summary>Reads options written as an array of <c>{ const, title?, description? }</c> objects.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="list">The <c>oneOf</c> or <c>anyOf</c> value.</param>
    /// <param name="kind">SingleSelect or MultiSelect.</param>
    /// <param name="problem">Why the options are refused, or <see langword="null"/>.</param>
    private static Shape? ReadConstOptions(string key, JsonElement list, ElicitationFieldKind kind, out string? problem)
    {
        if (!CheckOptionCount(key, list, out problem))
        {
            return null;
        }

        List<ElicitationOption> options = [];
        foreach (JsonElement item in list.EnumerateArray())
        {
            string? value = item.ValueKind == JsonValueKind.Object ? TryGetString(item, "const") : null;
            if (value is null)
            {
                problem = $"Property '{key}' has an option that is not a string constant.";
                return null;
            }

            options.Add(new ElicitationOption(value, TryGetString(item, "title") ?? value, TryGetString(item, "description")));
        }

        problem = null;
        return new Shape(kind, options, null, null, null, null);
    }

    /// <summary>Reads options written as an <c>enum</c> array of strings.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="list">The <c>enum</c> value.</param>
    /// <param name="kind">SingleSelect or MultiSelect.</param>
    /// <param name="problem">Why the options are refused, or <see langword="null"/>.</param>
    private static Shape? ReadEnumOptions(string key, JsonElement list, ElicitationFieldKind kind, out string? problem)
    {
        if (!CheckOptionCount(key, list, out problem))
        {
            return null;
        }

        List<ElicitationOption> options = [];
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                problem = $"Property '{key}' has an option that is not a string constant.";
                return null;
            }

            string value = item.GetString() ?? string.Empty;
            options.Add(new ElicitationOption(value, value, null));
        }

        problem = null;
        return new Shape(kind, options, null, null, null, null);
    }

    /// <summary>Checks that <paramref name="list"/> is a non-empty array of at most <see cref="MaxOptions"/> entries.</summary>
    /// <param name="key">The property name.</param>
    /// <param name="list">The options value.</param>
    /// <param name="problem">Why the list is refused, or <see langword="null"/> when it is within bounds.</param>
    private static bool CheckOptionCount(string key, JsonElement list, out string? problem)
    {
        int count = list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;
        if (count == 0)
        {
            problem = $"Property '{key}' has no options.";
            return false;
        }

        if (count > MaxOptions)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"Property '{key}' has {count} options; the limit is {MaxOptions}.");
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>
    /// Builds the field from its shape and the texts around it. An AskUserQuestion question
    /// (<c>question_n</c>) is labelled with its description - or, in a one-question form, with the
    /// request's own prompt - and keeps its title as the header; its <c>_custom</c> companion is tied to it.
    /// </summary>
    /// <param name="key">The property name.</param>
    /// <param name="node">The property's schema.</param>
    /// <param name="shape">What the property asks for.</param>
    /// <param name="context">What the whole form says about this property.</param>
    private static ElicitationField BuildField(string key, JsonElement node, Shape shape, FormContext context)
    {
        string? title = TryGetString(node, "title");
        string? description = TryGetString(node, "description");
        bool required = context.Required.Contains(key);

        if (QuestionKeyRegex().IsMatch(key))
        {
            string? prompt = context.QuestionCount == 1 ? context.Message : null;
            string label = description ?? prompt ?? title ?? key;
            return new ElicitationField(key, label, null, shape.Kind, shape.Options, required, shape.Minimum, shape.Maximum, shape.MinLength, shape.MaxLength, null, title);
        }

        string? customFor = shape.Kind == ElicitationFieldKind.Text ? CustomAnswerTarget(node, key, context.Keys) : null;
        return new ElicitationField(key, title ?? key, description, shape.Kind, shape.Options, required, shape.Minimum, shape.Maximum, shape.MinLength, shape.MaxLength, customFor);
    }

    /// <summary>
    /// The question a field is the typed "Other" answer of, when its <c>_meta</c> says so and that
    /// question is a property of the same form; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="node">The property's schema.</param>
    /// <param name="key">The property name.</param>
    /// <param name="keys">Every property name of the form.</param>
    private static string? CustomAnswerTarget(JsonElement node, string key, HashSet<string> keys)
    {
        if (node.TryGetProperty("_meta", out JsonElement meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("_askUserQuestionCustomAnswer", out JsonElement marker)
            && marker.ValueKind == JsonValueKind.Object
            && TryGetString(marker, "questionId") is { } target
            && keys.Contains(target)
            && !string.Equals(target, key, StringComparison.Ordinal))
        {
            return target;
        }

        return null;
    }

    /// <summary>
    /// Keeps only the custom-answer ties that can work: a field may not be tied to another custom
    /// field, and a question has at most one companion (the first); any other claim is a plain text field.
    /// </summary>
    /// <param name="fields">The fields as read.</param>
    private static List<ElicitationField> UntangleCustomFields(List<ElicitationField> fields)
    {
        HashSet<string> companions = [.. fields.Where(field => field.CustomAnswerFor is not null).Select(field => field.Key)];
        HashSet<string> claimed = new(StringComparer.Ordinal);
        List<ElicitationField> result = [];
        foreach (ElicitationField field in fields)
        {
            bool keeps = field.CustomAnswerFor is { } target && !companions.Contains(target) && claimed.Add(target);
            result.Add(field.CustomAnswerFor is not null && !keeps ? field with { CustomAnswerFor = null } : field);
        }

        return result;
    }

    /// <summary>The keys the top-level <c>required</c> array names; empty when it is absent or not an array of strings.</summary>
    /// <param name="root">The schema's top-level object.</param>
    private static HashSet<string> RequiredKeys(JsonElement root)
    {
        HashSet<string> required = new(StringComparer.Ordinal);
        if (root.TryGetProperty("required", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name)
                {
                    _ = required.Add(name);
                }
            }
        }

        return required;
    }

    /// <summary>The first schema keyword the bridge refuses (<c>$ref</c>, <c>allOf</c>) that <paramref name="node"/> uses, or <see langword="null"/>.</summary>
    /// <param name="node">The schema object to look at.</param>
    private static string? UnsupportedKeyword(JsonElement node)
    {
        if (node.TryGetProperty("$ref", out _))
        {
            return "$ref";
        }

        return node.TryGetProperty("allOf", out _) ? "allOf" : null;
    }

    /// <summary>Whether <paramref name="node"/> is the JSON string <paramref name="expected"/>.</summary>
    /// <param name="node">The value to test.</param>
    /// <param name="expected">The text it must hold.</param>
    private static bool IsString(JsonElement node, string expected)
    {
        return node.ValueKind == JsonValueKind.String && string.Equals(node.GetString(), expected, StringComparison.Ordinal);
    }

    /// <summary>Reads a string member, or <see langword="null"/> when it is absent or not a string.</summary>
    /// <param name="node">The object to read from.</param>
    /// <param name="name">The member name.</param>
    private static string? TryGetString(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>Reads a whole-number member, or <see langword="null"/> when it is absent or not one.</summary>
    /// <param name="node">The object to read from.</param>
    /// <param name="name">The member name.</param>
    private static int? TryGetInt(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)
            ? number
            : null;
    }

    /// <summary>Reads a numeric member, or <see langword="null"/> when it is absent or not a number.</summary>
    /// <param name="node">The object to read from.</param>
    /// <param name="name">The member name.</param>
    private static double? TryGetDouble(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)
            ? number
            : null;
    }

    /// <summary>Matches the Adapter's AskUserQuestion property names: <c>question_0</c>, <c>question_1</c>, and so on.</summary>
    [GeneratedRegex(@"\Aquestion_[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex QuestionKeyRegex();

    /// <summary>What a property asks for, before its texts are attached.</summary>
    /// <param name="Kind">The field kind.</param>
    /// <param name="Options">The choices of a select.</param>
    /// <param name="Minimum">The lower numeric bound.</param>
    /// <param name="Maximum">The upper numeric bound.</param>
    /// <param name="MinLength">The shortest text.</param>
    /// <param name="MaxLength">The longest text.</param>
    private sealed record Shape(
        ElicitationFieldKind Kind,
        IReadOnlyList<ElicitationOption> Options,
        double? Minimum,
        double? Maximum,
        int? MinLength,
        int? MaxLength);

    /// <summary>What the whole form says about one of its properties.</summary>
    /// <param name="Message">The request's prompt text.</param>
    /// <param name="Keys">Every property name of the form.</param>
    /// <param name="Required">The keys the <c>required</c> list names.</param>
    /// <param name="QuestionCount">How many properties are AskUserQuestion questions.</param>
    private sealed record FormContext(string Message, HashSet<string> Keys, HashSet<string> Required, int QuestionCount);
}
