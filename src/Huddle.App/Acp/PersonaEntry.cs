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
public sealed record PersonaEntry(
    string Name,
    string Title,
    string Alias,
    IReadOnlyList<string> Teams,
    string Path,
    string Text);
