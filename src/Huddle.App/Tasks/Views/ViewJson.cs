using Agency.Huddle.App.Library;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>JSON options and converters for <c>views.json</c>.</summary>
internal static class ViewJson
{
    // views.json is a file the Human hand-edits, never wire JSON, so this derives from
    // ProtocolJson.Options (traps.md) with UnsafeRelaxedJsonEscaping rather than the default
    // encoder: an escaped em-dash or angle bracket in a saved View name would read as corruption.
    // ProtocolJson.Options already carries a JsonStringEnumConverter(CamelCase), which is right
    // for ViewScope, TaskGroupField and SortDirection ("active", "descending", ...).
    // TaskState and TaskPriority do not follow a camelCase-of-the-identifier shape on the wire
    // ("In Progress", not "inProgress"), and ViewKind still reads its old name "board", so their
    // own converters are inserted ahead of the generic one to win the match.
    /// <summary>Options used to serialise and deserialise Views.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Builds <see cref="Options"/>.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(StoreJson.Indented);
        options.Converters.Insert(0, new TaskPriorityJsonConverter());
        options.Converters.Insert(0, new TaskStateJsonConverter());
        options.Converters.Insert(0, new ViewKindJsonConverter());
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
