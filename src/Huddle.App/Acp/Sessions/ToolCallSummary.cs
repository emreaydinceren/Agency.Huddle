using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Turns a tool call's raw input and output JSON into the short text carried by
/// <see cref="ToolActivity.Arguments"/> and <see cref="ToolActivity.Result"/>.
/// </summary>
internal static class ToolCallSummary
{
    private static readonly JsonWriterOptions CompactOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The call's input as compact JSON, clipped to <see cref="ToolActivityLimits.MaxArgumentsLength"/>.
    /// </summary>
    /// <param name="rawInputJson">The input JSON the Adapter sent, or <see langword="null"/>.</param>
    /// <returns>
    /// The compact JSON, or <see langword="null"/> when the input is missing, is not JSON or is an empty
    /// object (the Adapter's start of a streamed call), so a value stored earlier is left alone.
    /// </returns>
    public static string? Arguments(string? rawInputJson)
    {
        if (string.IsNullOrWhiteSpace(rawInputJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawInputJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().Any())
            {
                return null;
            }

            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, CompactOptions))
            {
                root.WriteTo(writer);
            }

            return ToolActivityLimits.Clip(Encoding.UTF8.GetString(stream.ToArray()), ToolActivityLimits.MaxArgumentsLength, out _);
        }
        catch (JsonException)
        {
            // Input that is not JSON says nothing the log can use, so there are no arguments.
            return null;
        }
    }

    /// <summary>
    /// The text an App Tool returned, clipped to <see cref="ToolActivityLimits.MaxResultLength"/>.
    /// </summary>
    /// <param name="rawOutputJson">The output JSON the Adapter sent, or <see langword="null"/>.</param>
    /// <returns>
    /// A JSON string's value, or the <c>text</c> of every text block of a content array joined by a newline;
    /// <see langword="null"/> for anything else.
    /// </returns>
    public static string? Result(string? rawOutputJson)
    {
        if (string.IsNullOrWhiteSpace(rawOutputJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawOutputJson);
            string? text = document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString(),
                JsonValueKind.Array => JoinTextBlocks(document.RootElement),
                _ => null,
            };
            return ToolActivityLimits.Clip(text, ToolActivityLimits.MaxResultLength, out _);
        }
        catch (JsonException)
        {
            // Output that is not JSON carries no text block to record.
            return null;
        }
    }

    /// <summary>Joins the <c>text</c> of every element of <paramref name="blocks"/> that is a text block; <see langword="null"/> when there is none.</summary>
    private static string? JoinTextBlocks(JsonElement blocks)
    {
        List<string> texts = [];
        foreach (JsonElement block in blocks.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "text", StringComparison.Ordinal)
                && block.TryGetProperty("text", out JsonElement text)
                && text.ValueKind == JsonValueKind.String
                && text.GetString() is string value)
            {
                texts.Add(value);
            }
        }

        return texts.Count == 0 ? null : string.Join('\n', texts);
    }
}
