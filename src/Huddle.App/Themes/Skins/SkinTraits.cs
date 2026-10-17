namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The structural choices a skin makes, one per trait (architecture section 3). Every property defaults to today's
/// Huddle, so a skin that names no trait is Huddle's own structure. A value is added to an enum in the phase that lands
/// its CSS.
/// </summary>
/// <param name="Message">How a message is laid out.</param>
/// <param name="Send">How the send control looks.</param>
/// <param name="Status">How status and priority marks are drawn.</param>
/// <param name="Labels">The letter case of section labels.</param>
/// <param name="Composer">How the composer is framed.</param>
/// <param name="Nav">How the current navigation item is marked.</param>
/// <param name="Surface">How cards and panels are surfaced.</param>
/// <param name="Marks">How inline marks are drawn.</param>
/// <param name="Reading">How long-form reading views are set.</param>
internal sealed record SkinTraits(
    MessageLayout Message = MessageLayout.Bubble,
    SendStyle Send = SendStyle.Icon,
    StatusStyle Status = StatusStyle.Chip,
    LabelCase Labels = LabelCase.Sentence,
    ComposerStyle Composer = ComposerStyle.Box,
    NavStyle Nav = NavStyle.Tint,
    SurfaceStyle Surface = SurfaceStyle.Flat,
    MarkStyle Marks = MarkStyle.Plain,
    ReadingStyle Reading = ReadingStyle.Plain);
