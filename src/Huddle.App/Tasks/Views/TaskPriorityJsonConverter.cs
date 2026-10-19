using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Converts <see cref="TaskPriority"/> to and from its wire name ("Low", "High", ...).</summary>
internal sealed class TaskPriorityJsonConverter : JsonConverter<TaskPriority>
{
    /// <summary>Reads a wire name and parses it into a <see cref="TaskPriority"/>.</summary>
    public override TaskPriority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? wire = reader.GetString();
        if (!TaskPriorities.TryParse(wire, out TaskPriority priority))
        {
            throw new JsonException($"Unknown task priority '{wire}'.");
        }

        return priority;
    }

    /// <summary>Writes a <see cref="TaskPriority"/> as its wire name.</summary>
    public override void Write(Utf8JsonWriter writer, TaskPriority value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToWire());
    }
}
