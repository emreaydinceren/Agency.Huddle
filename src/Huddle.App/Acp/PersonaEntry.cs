namespace Agency.Huddle.App.Acp;

/// <summary>
/// One Persona that loaded cleanly out of the Teams directory: its structural identity (mirroring
/// <see cref="PersonaIdentity"/>'s own fields) plus where it was actually discovered on disk and its
/// raw file text, cached here so <see cref="PersonaStore.Get(string)"/> is a pure lookup rather than
/// a re-read of the file. <see cref="Path"/> may be nested under any number of Team sub-folders -
/// <see cref="PersonaIndex"/> makes no assumption that it matches <c>{Name}.md</c> at the Teams
/// root, which is exactly the bug this type exists to fix: before it, a Persona's path was always
/// recomputed from its Name, so one discovered only under a Team sub-folder was listed but
/// unreachable.
/// </summary>
/// <param name="Name">The Persona's identity, from its frontmatter. See <see cref="PersonaIdentity.Name"/>.</param>
/// <param name="Title">See <see cref="PersonaIdentity.Title"/>.</param>
/// <param name="Alias">See <see cref="PersonaIdentity.Alias"/>.</param>
/// <param name="Teams">See <see cref="PersonaIdentity.Teams"/>.</param>
/// <param name="Path">The absolute path this Persona was actually discovered at.</param>
/// <param name="Text">The Persona's full raw file text (frontmatter and body), cached at discovery time.</param>
/// <param name="Adapter">See <see cref="PersonaIdentity.Adapter"/>. Carried through unchanged from the parsed identity so <see cref="PersonaStore.Get(string)"/> can join it onto <see cref="Persona.Adapter"/> without re-parsing the file.</param>
/// <param name="Skills">See <see cref="PersonaIdentity.Skills"/>. Carried through unchanged from the parsed identity, never <see langword="null"/> once populated by <see cref="PersonaIndex.Build"/>.</param>
/// <param name="Builtin">See <see cref="PersonaIdentity.Builtin"/>. Carried through unchanged from the parsed identity.</param>
public sealed record PersonaEntry(
    string Name,
    string Title,
    string Alias,
    IReadOnlyList<string> Teams,
    string Path,
    string Text,
    string? Adapter = null,
    IReadOnlyList<string>? Skills = null,
    string? Builtin = null);
