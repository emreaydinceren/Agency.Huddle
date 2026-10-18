namespace Agency.Huddle.SkillPacks;

/// <summary>The only way the library touches a disk (§6.2). <see cref="PhysicalFileReader"/> implements it over
/// <c>System.IO</c>; tests use an in-memory reader.</summary>
public interface IFileReader
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Whether a folder exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary>The file's text, decoded as UTF-8 (a BOM is accepted).</summary>
    string ReadAllText(string path);

    /// <summary>The file's bytes.</summary>
    byte[] ReadAllBytes(string path);

    /// <summary>The file's length in bytes.</summary>
    long Length(string path);

    /// <summary>When the file was last written.</summary>
    DateTimeOffset LastWriteTime(string path);

    /// <summary>The full paths of the files in <paramref name="directory"/>, in ordinal order.</summary>
    IReadOnlyList<string> EnumerateFiles(string directory, bool recursive);

    /// <summary>The full paths of the folders directly in <paramref name="directory"/>, in ordinal order.</summary>
    IReadOnlyList<string> EnumerateDirectories(string directory);

    /// <summary>The file attributes, for <see cref="FileAttributes.ReparsePoint"/> (S004).</summary>
    FileAttributes Attributes(string path);
}
