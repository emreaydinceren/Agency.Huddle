using System.Text;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>Writes text files the way Huddle expects them: UTF-8 without a byte-order mark, LF line endings, a final newline.</summary>
internal static class SeedIo
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating the folder first.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The content; line endings are normalised and a final newline is added.</param>
    internal static void WriteText(string path, string text)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string normalised = text.ReplaceLineEndings("\n");
        if (!normalised.EndsWith('\n'))
        {
            normalised += "\n";
        }

        File.WriteAllText(path, normalised, Utf8NoBom);
    }

    /// <summary>Combines a data folder with a <c>/</c>-separated relative path.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="relative">A path such as <c>Teams/Platform/Architecture.md</c>.</param>
    /// <returns>The full path.</returns>
    internal static string Resolve(string dataDir, string relative) =>
        Path.Combine(dataDir, relative.Replace('/', Path.DirectorySeparatorChar));
}
