namespace Agency.Huddle.App.Components;

/// <summary>
/// The shape of a <c>TextField</c>: one line, a 28 px toolbar line, or a multi-line box for long text (ComponentCatalog, TextField;
/// <c>Q-B3-1</c>). One value, not three flags, so a compact multi-line field cannot be asked for. Public because it is a component
/// parameter's type, and in <c>Agency.Huddle.App.Components</c> like <see cref="ButtonRole"/>, because a public type in a namespace
/// ending <c>Shared</c> fails CA1716.
/// </summary>
public enum TextFieldShape
{
    /// <summary>One line: the 32 px dense outlined box with 14 px text and a floating label. The default.</summary>
    Line,

    /// <summary>One line in a toolbar: a 28 px box with 13 px text and no floating label, level with the buttons and chips beside it.</summary>
    Compact,

    /// <summary>Editable long text (a reason, a description, instructions): a 4-row box in body1, 14 px at 400 with the body line height.</summary>
    LongText,

    /// <summary>Long text the Human writes as Markdown (a persona, a prompt, a Task description): the 4-row box in monospace at 13 px.</summary>
    Markdown,
}
