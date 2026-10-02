using System.Text.Json.Nodes;

namespace Agency.Huddle.MockAdapter;

/// <summary>
/// One form the mock can put to the Human: the prompt marker that asks for it and the
/// <c>elicitation/create</c> parameters it sends. The shapes are the ones the vendored adapter
/// (<c>claude-agent-acp</c>, <c>dist/elicitation.js</c>) builds for an AskUserQuestion, a model-refusal
/// dialog and an MCP form, so the Elicitation card sees on a free run what it will see from a real model.
/// </summary>
/// <param name="Marker">The exact, case-sensitive text in a prompt that asks for this form.</param>
/// <param name="Message">The form's prompt text.</param>
/// <param name="ToolCallId">The triggering tool call id, or <see langword="null"/> when the real request carries none.</param>
/// <param name="SchemaJson">The form's <c>requestedSchema</c>, as JSON text.</param>
internal sealed record MockElicitation(string Marker, string Message, string? ToolCallId, string SchemaJson)
{
    private const string AskToolCallId = "toolu_mock_ask";

    /// <summary>An AskUserQuestion with two questions: a single-select (Colour) and a multi-select (Extras), each followed by its own "Other" field.</summary>
    private static readonly MockElicitation Ask = new(
        "[elicit:ask]",
        "Please answer the following questions.",
        MockElicitation.AskToolCallId,
        """{"type":"object","properties":{"question_0":{"type":"string","title":"Colour","description":"Which colour do you prefer?","oneOf":[{"const":"Red","title":"Red","description":"Warm and bold"},{"const":"Green","title":"Green","description":"Calm and natural"},{"const":"Blue","title":"Blue","description":"Cool and steady"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}},"question_1":{"type":"array","title":"Extras","description":"Which extras do you want?","items":{"anyOf":[{"const":"Rugs","title":"Rugs","description":"Soft underfoot"},{"const":"Lamps","title":"Lamps","description":"Warm light"},{"const":"Plants","title":"Plants","description":"Something green"}]}},"question_1_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_1","isCustomAnswer":true}}}}}""");

    /// <summary>An AskUserQuestion with one single-select question: the question is the message and the field carries no description.</summary>
    private static readonly MockElicitation One = new(
        "[elicit:one]",
        "Which colour do you prefer?",
        MockElicitation.AskToolCallId,
        """{"type":"object","properties":{"question_0":{"type":"string","title":"Colour","oneOf":[{"const":"Red","title":"Red","description":"Warm and bold"},{"const":"Green","title":"Green","description":"Calm and natural"},{"const":"Blue","title":"Blue","description":"Cool and steady"}]},"question_0_custom":{"type":"string","title":"Other","description":"Type your own answer instead of choosing an option above (optional).","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0","isCustomAnswer":true}}}}}""");

    /// <summary>An MCP-style typed form with every supported field kind, three of them required and no tool call id.</summary>
    private static readonly MockElicitation Form = new(
        "[elicit:form]",
        "Plan the delivery",
        null,
        """{"type":"object","properties":{"title":{"type":"string","title":"Title"},"due":{"type":"string","format":"date","title":"Due date"},"count":{"type":"integer","minimum":1,"maximum":20,"title":"Boxes"},"budget":{"type":"number","minimum":0,"title":"Budget"},"agree":{"type":"boolean","title":"Insured"},"priority":{"type":"string","title":"Priority","oneOf":[{"const":"low","title":"Low"},{"const":"high","title":"High"}]},"tags":{"type":"array","title":"Handling","items":{"type":"string","anyOf":[{"const":"fragile","title":"Fragile"},{"const":"heavy","title":"Heavy"},{"const":"cold","title":"Cold chain"}]}},"notes":{"type":"string","title":"Notes","description":"Anything else we should know?"}},"required":["title","due","count"]}""");

    /// <summary>The refusal-fallback dialog: retry on the fallback model or keep the refusal.</summary>
    private static readonly MockElicitation Refusal = new(
        "[elicit:refusal]",
        "Model X declined this request. Retry with Model Y?",
        null,
        """{"type":"object","properties":{"choice":{"type":"string","oneOf":[{"const":"retry_fallback","title":"Retry with Model Y"},{"const":"cancelled","title":"Keep the refusal"}]}}}""");

    /// <summary>A form the app cannot render (its only property is a nested object), so the app is expected to answer decline.</summary>
    private static readonly MockElicitation Unsupported = new(
        "[elicit:unsupported]",
        "Where should we deliver?",
        null,
        """{"type":"object","properties":{"address":{"type":"object","title":"Address","properties":{"street":{"type":"string","title":"Street"},"city":{"type":"string","title":"City"}}}}}""");

    private static readonly MockElicitation[] All = [MockElicitation.Ask, MockElicitation.One, MockElicitation.Form, MockElicitation.Refusal, MockElicitation.Unsupported];

    /// <summary>
    /// Finds the form a prompt asks for: the marker that appears first in <paramref name="prompt"/>, matched
    /// exactly (case-sensitive) anywhere in the text.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The form to put to the Human, or <see langword="null"/> when the prompt carries no marker.</returns>
    internal static MockElicitation? Find(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        MockElicitation? found = null;
        int foundAt = int.MaxValue;
        foreach (MockElicitation candidate in MockElicitation.All)
        {
            int index = prompt.IndexOf(candidate.Marker, StringComparison.Ordinal);
            if (index >= 0 && index < foundAt)
            {
                found = candidate;
                foundAt = index;
            }
        }

        return found;
    }

    /// <summary>Builds this form's <c>requestedSchema</c> as a fresh object, so a caller cannot change the shared definition.</summary>
    /// <returns>The schema object.</returns>
    internal JsonObject BuildSchema()
    {
        return JsonNode.Parse(this.SchemaJson) as JsonObject
            ?? throw new InvalidOperationException($"The schema of {this.Marker} is not a JSON object.");
    }
}
