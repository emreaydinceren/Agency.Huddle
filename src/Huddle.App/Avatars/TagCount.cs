namespace Agency.Huddle.App.Avatars;

/// <summary>One entry of the tag chip pool: a tag and how many library avatars carry it.</summary>
/// <param name="Tag">The tag, in the casing it was first seen with.</param>
/// <param name="Count">The number of avatars carrying the tag; an avatar counts once per tag.</param>
internal sealed record TagCount(string Tag, int Count);
