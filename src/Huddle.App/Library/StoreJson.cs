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
    /// <remarks>
    /// <para>
    /// "Unsafe" names the HTML/script-injection risk the encoder accepts, and no such context exists for a file
    /// that only ever round-trips through <see cref="JsonSerializer"/>. The default encoder would be worse than
    /// noisy: a Human who types a literal <c>&lt;name&gt;</c> into a file would see the app rewrite both angle
    /// brackets as <c>\uXXXX</c> escapes on the next save, which reads as corruption.
    /// </para>
    /// <para>
    /// The relaxed encoder does not reach a character outside the Basic Multilingual Plane - most full-colour
    /// emoji. <see cref="System.Text.Json"/> always writes one as a <c>\uXXXX\uXXXX</c> surrogate pair, for every
    /// built-in and custom encoder alike (even <c>JavaScriptEncoder.Create(UnicodeRanges.All)</c>), so such a
    /// value is stored and read back correctly but keeps that one cosmetic escape on disk.
    /// </para>
    /// </remarks>
    public static JsonSerializerOptions Indented { get; } = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
