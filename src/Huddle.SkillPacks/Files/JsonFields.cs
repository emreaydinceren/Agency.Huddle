using System.Text.Json;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>Reads JSON properties by name ignoring case, as the reading rules require (§6.2).</summary>
internal static class JsonFields
{
    /// <summary>Parses <paramref name="json"/>, tolerating a BOM; null when it is not JSON.</summary>
    public static JsonDocument? TryParse(string json)
    {
        string text = json.Length > 0 && json[0] == '﻿' ? json[1..] : json;
        try
        {
            return JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = 64 });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The property named <paramref name="name"/>, compared ignoring case, or null.</summary>
    public static JsonElement? Get(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    /// <summary>The property's string value, or null when absent, null or not a string.</summary>
    public static string? String(JsonElement element, string name)
    {
        JsonElement? value = Get(element, name);
        return value is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;
    }

    /// <summary>Whether the property is present and is not a string (a type error).</summary>
    public static bool IsWrongType(JsonElement element, string name, JsonValueKind expected)
    {
        JsonElement? value = Get(element, name);
        return value is { } present && present.ValueKind != expected && present.ValueKind != JsonValueKind.Null;
    }

    /// <summary>The property's integer value, or null.</summary>
    public static int? Int(JsonElement element, string name)
    {
        JsonElement? value = Get(element, name);
        return value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt32(out int result) ? result : null;
    }

    /// <summary>The property's strings, skipping anything that is not a string; empty when absent.</summary>
    public static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        JsonElement? value = Get(element, name);
        if (value is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        List<string> items = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
            {
                items.Add(text);
            }
        }

        return items;
    }
}
