using Agency.Huddle.Contracts;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Agency.Huddle.App.Library;

/// <summary>JSON options shared by the stores that keep a Human-readable file on disk.</summary>
internal static class StoreJson
{
    /// <summary>
    /// Derived from <see cref="ProtocolJson.Options"/> (traps.md), so the naming policy, the camelCase enum
    /// converter and <c>DefaultIgnoreCondition = WhenWritingNull</c> match the wire. Indented because a Human
    /// hand-edits these files, and with <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> because the
    /// files only round-trip through <see cref="JsonSerializer"/>, never through HTML or script, so an em-dash
    /// or angle bracket stays readable instead of becoming <c>\uXXXX</c>. Copy it with
    /// <c>new(StoreJson.Indented)</c> to add converters; never mutate it.
    /// </summary>
    public static JsonSerializerOptions Indented { get; } = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
