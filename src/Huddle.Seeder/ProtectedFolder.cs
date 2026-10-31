namespace Agency.Huddle.Seeder;

/// <summary>A folder the seeder must never wipe. Seeder spec §4 (SEED-SAFE-3).</summary>
/// <param name="Path">The folder.</param>
/// <param name="BlocksDescendants">
/// <see langword="true"/> when nothing inside the folder may be wiped either (the repository);
/// <see langword="false"/> when only the folder itself and its ancestors are protected (the user profile,
/// so a seed root under the profile or the temp folder is still allowed).
/// </param>
internal sealed record ProtectedFolder(string Path, bool BlocksDescendants);
