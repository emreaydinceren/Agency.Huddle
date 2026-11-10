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
/// <param name="Voice">The type voice of headings, outlines and prose.</param>
/// <param name="Ids">How a Task id is set.</param>
internal sealed record SkinTraits(
    MessageLayout Message = MessageLayout.Bubble,
    SendStyle Send = SendStyle.Icon,
    StatusStyle Status = StatusStyle.Glyph,
    LabelCase Labels = LabelCase.Sentence,
    ComposerStyle Composer = ComposerStyle.Box,
    NavStyle Nav = NavStyle.Tint,
    SurfaceStyle Surface = SurfaceStyle.Flat,
    VoiceStyle Voice = VoiceStyle.Plain,
    IdStyle Ids = IdStyle.Plain)
{
    /// <summary>
    /// The nine traits as the <c>data-hu-*</c> attributes the layout's Theme marker carries: the attribute name is
    /// <c>data-hu-</c> plus the trait's lower-case name, the value its kebab-case value name.
    /// </summary>
    /// <returns>Attribute name to value, in trait declaration order.</returns>
    public IReadOnlyDictionary<string, object> ToMarkerAttributes() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            ["data-hu-message"] = SkinLoader.Kebab(this.Message.ToString()),
            ["data-hu-send"] = SkinLoader.Kebab(this.Send.ToString()),
            ["data-hu-status"] = SkinLoader.Kebab(this.Status.ToString()),
            ["data-hu-labels"] = SkinLoader.Kebab(this.Labels.ToString()),
            ["data-hu-composer"] = SkinLoader.Kebab(this.Composer.ToString()),
            ["data-hu-nav"] = SkinLoader.Kebab(this.Nav.ToString()),
            ["data-hu-surface"] = SkinLoader.Kebab(this.Surface.ToString()),
            ["data-hu-voice"] = SkinLoader.Kebab(this.Voice.ToString()),
            ["data-hu-ids"] = SkinLoader.Kebab(this.Ids.ToString()),
#pragma warning restore HAA0102
        };
}
