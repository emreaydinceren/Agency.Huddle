using System.Text.Json;

namespace Agency.Huddle.App.Library;

/// <summary>Reads a JSON object front matter (D-9) into properties with the same rules as YAML: an array of scalars is a list, an object is Unsupported, and <c>true</c>, <c>false</c> and numbers infer as their types. A string is a quoted scalar, so it is Text.</summary>
internal static class NoteJsonProperties
{
    /// <summary>Reads the members of the object in <paramref name="json"/>; empty when it is not a JSON object.</summary>
    /// <param name="json">The block's text.</param>
    internal static List<NoteProperty> Read(string json)
    {
        List<NoteProperty> properties = [];
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return properties;
            }

            foreach (JsonProperty member in document.RootElement.EnumerateObject())
            {
                properties.Add(ReadMember(member));
            }
        }
        catch (JsonException)
        {
            // Not an object after all: a note whose block is not JSON has no properties, and the reader never fails the note (D-10).
            properties.Clear();
        }

        return properties;
    }

    private static NoteProperty ReadMember(JsonProperty member)
    {
        JsonElement value = member.Value;
        string source = value.GetRawText();
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                return ReadArray(member.Name, value, source);
            case JsonValueKind.Object:
                return Unsupported(member.Name, source);
            case JsonValueKind.String:
                return ReadString(member.Name, value.GetString() ?? string.Empty, source);
            case JsonValueKind.True or JsonValueKind.False:
                return new NoteProperty(member.Name, NotePropertyKind.Checkbox, NotePropertyShape.Scalar, source, [], source);
            case JsonValueKind.Number:
                return new NoteProperty(member.Name, NotePropertyKind.Number, NotePropertyShape.Scalar, source, [], source);
            default:
                return new NoteProperty(member.Name, NotePropertyKind.Text, NotePropertyShape.Empty, string.Empty, [], source);
        }
    }

    /// <summary>A JSON string is a quoted value, so it is Text whatever it looks like; an explicit date type in the types file still reads it as a Date, as it does a quoted YAML value.</summary>
    private static NoteProperty ReadString(string key, string text, string source)
    {
        (NotePropertyKind kind, NotePropertyShape shape) = NoteYamlScalars.Infer(text, quoted: true);
        return new NoteProperty(key, kind, shape, text, [], source);
    }

    private static NoteProperty ReadArray(string key, JsonElement array, string source)
    {
        List<string> items = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    items.Add(item.GetString() ?? string.Empty);
                    break;
                case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                    items.Add(item.GetRawText());
                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    return Unsupported(key, source);
            }
        }

        return new NoteProperty(key, NotePropertyKind.List, NotePropertyShape.List, string.Empty, items, source);
    }

    private static NoteProperty Unsupported(string key, string source) =>
        new(key, NotePropertyKind.Unsupported, NotePropertyShape.Nested, string.Empty, [], source);
}
