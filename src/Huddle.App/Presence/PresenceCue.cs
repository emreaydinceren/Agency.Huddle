using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Presence;

/// <summary>
/// What a presence badge or <c>StatusDot</c> shows: the <see cref="Glyph"/>, a CSS <see cref="Tone"/> class
/// that carries the colour, and the state's <see cref="Label"/>. Both status systems map into this one shape, so the
/// four glyphs are the whole visual vocabulary: <see cref="PersonaState"/> (Health: can the Adapter act at all) and
/// <see cref="PresenceState"/> (Presence: what the assignee is doing right now).
/// </summary>
/// <param name="Glyph">The shape drawn in the badge.</param>
/// <param name="Tone">
/// The lower-case CSS class that colours the badge: <c>online</c> (green), <c>starting</c> and <c>awake</c> (amber),
/// <c>degraded</c> (red), <c>offline</c> and <c>asleep</c> (the hollow grey ring). The words differ; the colours are those four.
/// </param>
/// <param name="Label">The state's name, used as the badge's accessible name and title.</param>
public readonly record struct PresenceCue(PresenceGlyph Glyph, string Tone, string Label)
{
    /// <summary>The cue for a Persona's Health: Online a check, Starting a pulse, Degraded a dash, Offline a hollow ring.</summary>
    /// <param name="state">The Health state.</param>
    /// <returns>The cue to draw.</returns>
    public static PresenceCue From(PersonaState state) => state switch
    {
        PersonaState.Online => new(PresenceGlyph.Check, "online", "Online"),
        PersonaState.Starting => new(PresenceGlyph.Pulse, "starting", "Starting"),
        PersonaState.Degraded => new(PresenceGlyph.Dash, "degraded", "Degraded"),
        _ => new(PresenceGlyph.Ring, "offline", "Offline"),
    };

    /// <summary>The cue for an assignee's Presence: Awake (a Turn running) a pulse, Asleep (online, idle) and Offline a hollow ring.</summary>
    /// <param name="presence">The Presence state.</param>
    /// <returns>The cue to draw.</returns>
    public static PresenceCue From(PresenceState presence) => presence switch
    {
        PresenceState.Awake => new(PresenceGlyph.Pulse, "awake", "Awake"),
        PresenceState.Asleep => new(PresenceGlyph.Ring, "asleep", "Asleep"),
        _ => new(PresenceGlyph.Ring, "offline", "Offline"),
    };

    /// <summary>The cue for an Agent in goldfish mode: the fish glyph, in the Info tone, replacing its Health cue.</summary>
    /// <param name="name">The Agent's display name.</param>
    /// <returns>The cue to draw.</returns>
    public static PresenceCue Goldfish(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new(PresenceGlyph.Fish, "goldfish", $"{name}, in goldfish mode");
    }
}
