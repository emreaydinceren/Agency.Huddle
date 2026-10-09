namespace Agency.Huddle.App.Avatars;

/// <summary>The outcome of loading the Avatar Library index.</summary>
/// <param name="Catalog">The catalogue of valid records, or null when the index was unreadable.</param>
/// <param name="Skipped">How many records were dropped as unsafe, missing, not an image, oversize or duplicate.</param>
/// <param name="Unreadable">True when the library is not configured or <c>master.json</c> could not be used.</param>
internal sealed record AvatarLibraryLoadResult(AvatarCatalog? Catalog, int Skipped, bool Unreadable);
