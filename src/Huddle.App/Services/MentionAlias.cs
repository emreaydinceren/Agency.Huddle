namespace Agency.Huddle.App.Services;

/// <summary>
/// One Persona's Alias, paired with the Persona's Name it belongs to. This is the shape
/// <see cref="MentionParser"/> needs to fold an Alias into the same candidate list as a Member's Name -
/// an alias on its own is just a short string; it only becomes something a Mention can resolve once it
/// is paired with the Name whose Member it should resolve to. <see cref="IMentionAliasSource"/> is what
/// supplies these pairs, library-wide, to both <see cref="MentionParser"/> and <see cref="ChatService"/>.
/// </summary>
/// <param name="Alias">The short handle a Mention or a typed <c>/invite</c> command may use in place of <paramref name="Name"/>.</param>
/// <param name="Name">The Persona's Name that <paramref name="Alias"/> belongs to.</param>
public sealed record MentionAlias(string Alias, string Name);
