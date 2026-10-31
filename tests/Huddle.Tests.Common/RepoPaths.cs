using System.Runtime.CompilerServices;

namespace Agency.Huddle.Tests;

/// <summary>Paths into this checkout, for tests that read repo files.</summary>
internal static class RepoPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    /// <summary>The checkout root: the nearest ancestor of the test binaries holding <c>Huddle.slnx</c>. Computed once.</summary>
    internal static string Root => RootLazy.Value;

    /// <summary><see cref="Root"/> joined with <paramref name="segments"/>.</summary>
    /// <param name="segments">Path segments relative to the checkout root.</param>
    /// <returns>The combined path.</returns>
    internal static string Combine(params string[] segments)
    {
        string path = Root;
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    /// <summary><see cref="Root"/>/<c>src</c> joined with <paramref name="segments"/>.</summary>
    /// <param name="segments">Path segments relative to the <c>src</c> folder.</param>
    /// <returns>The combined path.</returns>
    internal static string Src(params string[] segments) => Combine(["src", .. segments]);

    /// <summary><see cref="Root"/>/<c>src</c>/<c>Huddle.App</c> joined with <paramref name="segments"/>.</summary>
    /// <param name="segments">Path segments relative to the <c>src/Huddle.App</c> folder.</param>
    /// <returns>The combined path.</returns>
    internal static string App(params string[] segments) => Src(["Huddle.App", .. segments]);

    /// <summary>A file or folder relative to the folder of the calling source file.</summary>
    /// <param name="relativePath">The path relative to the calling source file's folder.</param>
    /// <param name="callerFilePath">Filled in by the compiler with the calling source file.</param>
    /// <returns>The full path.</returns>
    internal static string Data(string relativePath, [CallerFilePath] string callerFilePath = "")
    {
        string folder = Path.GetDirectoryName(callerFilePath) ?? string.Empty;
        return Path.Combine(folder, relativePath);
    }

    /// <summary>Walks up from the test binaries to the folder holding <c>Huddle.slnx</c>.</summary>
    /// <returns>The checkout root.</returns>
    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
    }
}
