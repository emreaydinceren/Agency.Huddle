namespace Agency.Huddle.Seeder.Model;

/// <summary>A binary file under the data folder, such as a Library image or an avatar picture.</summary>
/// <param name="RelativePath">The path below the data folder, using <c>/</c>.</param>
/// <param name="Content">The bytes.</param>
internal sealed record SeedBinaryFile(string RelativePath, byte[] Content);
