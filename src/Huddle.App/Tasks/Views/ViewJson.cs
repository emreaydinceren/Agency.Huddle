using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>JSON options and converters for <c>views.json</c>.</summary>
internal static class ViewJson
{
    // views.json is a file the Human hand-edits, never wire JSON, so this derives from
    // ProtocolJson.Options (traps.md) with UnsafeRelaxedJsonEscaping rather than the default
    // encoder: an escaped em-dash or angle bracket in a saved View name would read as corruption.
    // ProtocolJson.Options already carries a JsonStringEnumConverter(CamelCase), which is right
    // for ViewKind, ViewScope, TaskGroupField and SortDirection ("board", "descending", ...).
    // TaskState and TaskPriority do not follow a camelCase-of-the-identifier shape on the wire
    // ("In Progress", not "inProgress"), so their own converters are inserted ahead of the
    // generic one to win the match.
    /// <summary>Options used to serialise and deserialise Views.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Builds <see cref="Options"/>.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(ProtocolJson.Options)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Insert(0, new TaskPriorityJsonConverter());
        options.Converters.Insert(0, new TaskStateJsonConverter());
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptySprints } };
        return options;
    }

    /// <summary>
    /// Leaves <see cref="TaskFilter.Sprints"/> out of the output when it is empty. <c>JsonIgnoreCondition.WhenWritingDefault</c>
    /// cannot do this because the default is an empty list, not null, and <c>Teams</c> and its siblings are always
    /// written; this keeps a View that never filters on Sprints byte-identical to what was written before Sprints existed.
    /// </summary>
    /// <param name="typeInfo">The type info being built.</param>
    private static void OmitEmptySprints(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(TaskFilter))
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (string.Equals(property.Name, "sprints", StringComparison.Ordinal))
            {
                property.ShouldSerialize = static (_, value) => value is IReadOnlyCollection<string> { Count: > 0 };
            }
        }
    }
}

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
