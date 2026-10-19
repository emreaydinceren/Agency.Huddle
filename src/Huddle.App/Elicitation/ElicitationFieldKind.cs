using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// What a field of an agent's form asks the Human for (elicitation bridge, decision E-6). Each kind is
/// one of the JSON Schema shapes the bridge can show faithfully; anything else is refused whole by
/// <c>ElicitationSchemaReader</c>, because a number or a boolean degraded to a text box would be sent
/// as a string and rejected by the server.
/// </summary>
public enum ElicitationFieldKind
{
    /// <summary>Free text: a <c>string</c> with no options, whatever its format except <c>date</c>.</summary>
    Text,

    /// <summary>A <c>number</c>, sent as a <see cref="double"/>.</summary>
    Number,

    /// <summary>An <c>integer</c>, sent as a <see cref="long"/>.</summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Integer is the JSON Schema type name this kind is read from, and what a reader of the schema expects it to be called; it names no CLR type here.")]
    Integer,

    /// <summary>A <c>boolean</c>, shown as Yes or No.</summary>
    Boolean,

    /// <summary>Exactly one of a fixed list of options: a <c>string</c> with <c>enum</c> or <c>oneOf</c>.</summary>
    SingleSelect,

    /// <summary>Any number of a fixed list of options: an <c>array</c> of string <c>enum</c> or <c>anyOf</c>.</summary>
    MultiSelect,

    /// <summary>A calendar date: a <c>string</c> with <c>format: date</c>, sent as <c>yyyy-MM-dd</c>.</summary>
    Date,
}
