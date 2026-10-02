using System.Globalization;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Elicitation;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// The literal form schemas the vendored Adapter (claude-agent-acp 0.75.1, <c>dist/elicitation.js</c>)
/// puts on the wire, and the small builders the Elicitation tests share. The schema texts are what
/// <c>askUserQuestionsToCreateRequest</c> and <c>refusalFallbackToCreateRequest</c> build, written as the
/// compact JSON the Acp layer hands over, so a reader or composer test runs against the real shape.
/// </summary>
internal static class ElicitationTestSupport
{
    /// <summary>One AskUserQuestion with a header, two options (one with a description) and the per-question custom field; the prompt rides in <see cref="SingleQuestionMessage"/>.</summary>
    internal const string SingleQuestionSchema = """{"type":"object","properties":{"question_0":{"type":"string","title":"DB","oneOf":[{"const":"Postgres","title":"Postgres","description":"Relational"},{"const":"SQLite","title":"SQLite"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}}}}""";

    /// <summary>The <c>message</c> that goes with <see cref="SingleQuestionSchema"/>: the question itself.</summary>
    internal const string SingleQuestionMessage = "Which database?";

    /// <summary>Two AskUserQuestions, a single select and a multi select, each with its custom field and its own description.</summary>
    internal const string TwoQuestionSchema = """{"type":"object","properties":{"question_0":{"type":"string","title":"DB","description":"Which database?","oneOf":[{"const":"Postgres","title":"Postgres","description":"Relational"},{"const":"SQLite","title":"SQLite"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}},"question_1":{"type":"array","title":"Features","description":"Which features?","items":{"anyOf":[{"const":"Auth","title":"Auth"},{"const":"Billing","title":"Billing"},{"const":"Search","title":"Search"}]}},"question_1_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_1","isCustomAnswer":true}}}}}""";

    /// <summary>The <c>message</c> that goes with <see cref="TwoQuestionSchema"/>.</summary>
    internal const string TwoQuestionMessage = "Please answer the following questions.";

    /// <summary>The refusal-fallback dialog: one <c>choice</c> property with the two result constants.</summary>
    internal const string RefusalSchema = """{"type":"object","properties":{"choice":{"type":"string","oneOf":[{"const":"retry_fallback","title":"Retry with Opus","description":"The session continues on Opus."},{"const":"cancelled","title":"Keep the refusal","description":"You can send a new message."}]}}}""";

    /// <summary>The <c>message</c> that goes with <see cref="RefusalSchema"/>.</summary>
    internal const string RefusalMessage = "Fable declined this request (cyber). Retry with Opus?";

    /// <summary>An MCP server's form: a bit of everything the bridge supports, with two required fields.</summary>
    internal const string McpSchema = """{"type":"object","properties":{"name":{"type":"string","title":"Name","description":"Your full name","minLength":2,"maxLength":40},"age":{"type":"integer","title":"Age","minimum":0,"maximum":120},"ratio":{"type":"number","title":"Ratio","minimum":0.5,"maximum":9.5},"agree":{"type":"boolean","title":"I agree"},"due":{"type":"string","title":"Due","format":"date"},"color":{"type":"string","title":"Color","enum":["red","green"]},"tags":{"type":"array","title":"Tags","items":{"type":"string","enum":["a","b","c"]}}},"required":["name","agree"]}""";

    /// <summary><see cref="SingleQuestionSchema"/> with its one question marked <c>required</c>, which the Adapter never does but a schema may.</summary>
    internal const string RequiredSingleQuestionSchema = """{"type":"object","properties":{"question_0":{"type":"string","title":"DB","oneOf":[{"const":"Postgres","title":"Postgres","description":"Relational"},{"const":"SQLite","title":"SQLite"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}}},"required":["question_0"]}""";

    /// <summary>Wraps a properties object body in the top-level schema the Adapter always sends.</summary>
    /// <param name="properties">The JSON between the braces of <c>"properties":{ }</c>.</param>
    /// <param name="required">The JSON array text of the top-level <c>required</c> list, or <see langword="null"/> for none.</param>
    internal static string Schema(string properties, string? required = null)
    {
        string requiredPart = required is null ? string.Empty : ",\"required\":" + required;
        return """{"type":"object","properties":{""" + properties + "}" + requiredPart + "}";
    }

    /// <summary>Builds a request as the Acp layer hands it to the bridge.</summary>
    /// <param name="schema">The form's JSON Schema text.</param>
    /// <param name="message">The prompt text the agent wants shown.</param>
    internal static ElicitationRequest Request(string schema, string message = "Pick one.")
    {
        return new ElicitationRequest("session-1", null, message, schema);
    }

    /// <summary>
    /// Describes <paramref name="field"/> as one line, every member spelled out, a null as <c>-</c>, so an
    /// assertion on the line fails with the difference in plain sight. Numbers use the invariant culture.
    /// </summary>
    /// <param name="field">The field to describe.</param>
    internal static string Describe(ElicitationField field)
    {
        string options = string.Join(";", field.Options.Select(option => $"{option.Value}/{option.Label}/{Spell(option.Description)}"));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{field.Key}|{field.Kind}|label={Spell(field.Label)}|desc={Spell(field.Description)}|header={Spell(field.Header)}|req={field.Required}|min={Spell(field.Minimum)}|max={Spell(field.Maximum)}|minLen={Spell(field.MinLength)}|maxLen={Spell(field.MaxLength)}|for={Spell(field.CustomAnswerFor)}|opts=[{options}]");
    }

    /// <summary>Spells a nullable value for <see cref="Describe"/>: <c>-</c> for null, the invariant text otherwise.</summary>
    /// <param name="value">The value to spell.</param>
    private static string Spell(object? value)
    {
        return value is null ? "-" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-";
    }

    /// <summary>Reads <paramref name="schema"/> into a form, failing the test when the reader refuses it.</summary>
    /// <param name="schema">The form's JSON Schema text.</param>
    /// <param name="message">The prompt text the agent wants shown.</param>
    internal static ElicitationForm FormOf(string schema, string message = "Pick one.")
    {
        bool read = ElicitationSchemaReader.TryRead(Request(schema, message), out ElicitationForm? form, out string? problem);
        Assert.True(read, problem);
        Assert.NotNull(form);
        return form;
    }

    /// <summary>One entry of the values the Human typed: a field key and the entries of that field.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="entries">Zero or one entries for a scalar field, the chosen option values for a multi select.</param>
    internal static KeyValuePair<string, IReadOnlyList<string>> Pair(string key, params string[] entries)
    {
        return new KeyValuePair<string, IReadOnlyList<string>>(key, entries);
    }

    /// <summary>Builds the values dictionary the composer takes.</summary>
    /// <param name="pairs">The entries, one per field the Human touched.</param>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Values(params KeyValuePair<string, IReadOnlyList<string>>[] pairs)
    {
        return new Dictionary<string, IReadOnlyList<string>>(pairs, StringComparer.Ordinal);
    }

    /// <summary>
    /// Parses a compact spec into values: <c>key=a|b;other=c</c> is two fields, the first with two
    /// entries. An empty spec is no values. Keys and entries never contain <c>;</c>, <c>=</c> or <c>|</c>.
    /// </summary>
    /// <param name="spec">The compact spec.</param>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseValues(string spec)
    {
        Dictionary<string, IReadOnlyList<string>> values = new(StringComparer.Ordinal);
        if (spec.Length == 0)
        {
            return values;
        }

        foreach (string part in spec.Split(';'))
        {
            int split = part.IndexOf('=');
            string key = part[..split];
            string rest = part[(split + 1)..];
            values[key] = rest.Length == 0 ? [] : rest.Split('|');
        }

        return values;
    }

    /// <summary>Runs <paramref name="action"/> with <paramref name="cultureName"/> as the current culture, then restores the previous one.</summary>
    /// <param name="cultureName">The culture to run under, for example <c>de-DE</c>.</param>
    /// <param name="action">The code to run.</param>
    internal static void UnderCulture(string cultureName, Action action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
