namespace Agency.Huddle.App.Avatars;

/// <summary>Reads the Avatar Library from disk on demand and keeps nothing.</summary>
internal interface IAvatarLibraryLoader
{
    /// <summary>Opens <c>master.json</c>, validates every record and returns a fresh catalogue.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The load result; never throws for a missing or malformed library.</returns>
    Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Reads one library image by bare file name.</summary>
    /// <param name="fileName">The image's bare file name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes and sniffed extension, or null when the name or the file is not acceptable.</returns>
    Task<AvatarLibraryImage?> ReadImageAsync(string fileName, CancellationToken cancellationToken);
}
