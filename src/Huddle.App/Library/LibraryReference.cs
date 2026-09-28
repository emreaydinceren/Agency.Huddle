namespace Agency.Huddle.App.Library;

/// <summary>A resolved link to a file inside a Library Root.</summary>
/// <param name="RootId">The root identifier (e.g., "teams", "teammates", or a pinned root slug).</param>
/// <param name="RelativePath">The relative path from the root, forward-slash, no leading slash.</param>
/// <param name="Exists">True if the linked file exists, false if the link is unresolved or the file was deleted.</param>
/// <param name="IsAmbiguous">True when several notes tied for a wikilink target (D9 item 39), so the UI can show
/// "Several notes match" instead of linking to one winner silently.</param>
public sealed record LibraryReference(string RootId, string RelativePath, bool Exists, bool IsAmbiguous = false);
