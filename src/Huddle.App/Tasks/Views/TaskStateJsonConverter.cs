using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>Converts <see cref="TaskState"/> to and from its wire name ("To Do", "In Progress", ...).</summary>
internal sealed class TaskStateJsonConverter : JsonConverter<TaskState>
{
    /// <summary>Reads a wire name and parses it into a <see cref="TaskState"/>.</summary>
    public override TaskState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? wire = reader.GetString();
        if (!TaskStates.TryParse(wire, out TaskState state))
        {
            throw new JsonException($"Unknown task state '{wire}'.");
        }

        return state;
    }

    /// <summary>Writes a <see cref="TaskState"/> as its wire name.</summary>
    public override void Write(Utf8JsonWriter writer, TaskState value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToWire());
    }
}
