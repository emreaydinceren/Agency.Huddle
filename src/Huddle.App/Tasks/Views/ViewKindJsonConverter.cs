using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>
/// Converts <see cref="ViewKind"/> to and from its wire name ("list", "kanban"). <see cref="ViewKind.Kanban"/> was
/// <c>Board</c> and was saved as "board", so that name is still read; it is never written again.
/// </summary>
internal sealed class ViewKindJsonConverter : JsonConverter<ViewKind>
{
    /// <summary>Reads a wire name, ignoring case, into a <see cref="ViewKind"/>.</summary>
    public override ViewKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? wire = reader.GetString();
        return wire?.ToUpperInvariant() switch
        {
            "LIST" => ViewKind.List,
            "KANBAN" or "BOARD" => ViewKind.Kanban,
            _ => throw new JsonException($"Unknown view kind '{wire}'."),
        };
    }

    /// <summary>Writes a <see cref="ViewKind"/> as its wire name.</summary>
    public override void Write(Utf8JsonWriter writer, ViewKind value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value switch
        {
            ViewKind.List => "list",
            ViewKind.Kanban => "kanban",
            _ => throw new JsonException($"Unknown view kind '{value}'."),
        });
    }
}
