namespace Agency.Huddle.SkillPacks;

/// <summary>An <see cref="IFileReader"/> that can also hash a Skill folder, so a caller can cache hashes by folder
/// (§8.4, "When hashing runs"). The resolver uses it when the reader it is handed implements it.</summary>
public interface IFolderHasher
{
    /// <summary>The <see cref="Folders.SkillFolderHash.ComputeFolder(IFileReader, string)"/> of <paramref name="folder"/>.</summary>
    string HashFolder(string folder);
}
