using System.Reflection;

namespace Agency.Huddle.App;

/// <summary>The one way the app reads files embedded in an assembly: missing resources throw, names use <c>/</c>, text uses <c>\n</c>.</summary>
internal static class EmbeddedResources
{
    /// <summary>Opens an embedded resource.</summary>
    /// <param name="assembly">The assembly the resource is embedded in.</param>
    /// <param name="name">The resource's manifest name.</param>
    /// <returns>A readable stream the caller disposes.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing or cannot be opened.</exception>
    internal static Stream Open(Assembly assembly, string name)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing or could not be opened.");
    }

    /// <summary>Reads one embedded resource as UTF-8 text with line endings normalised to <c>\n</c>.</summary>
    /// <param name="assembly">The assembly the resource is embedded in.</param>
    /// <param name="name">The resource's manifest name.</param>
    /// <returns>The resource's text.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing or cannot be opened.</exception>
    internal static string ReadText(Assembly assembly, string name)
    {
        using Stream stream = Open(assembly, name);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>Reads every embedded resource whose name, with <c>\</c> turned into <c>/</c>, starts with a prefix.</summary>
    /// <param name="assembly">The assembly the resources are embedded in.</param>
    /// <param name="prefix">The <c>/</c>-separated name prefix, compared ordinally.</param>
    /// <param name="include">Filters on the part of the name after the prefix; null keeps everything.</param>
    /// <returns>The part of the name after the prefix, to the resource's <c>\n</c>-normalised text; ordinal keys.</returns>
    /// <exception cref="InvalidOperationException">A listed resource cannot be opened.</exception>
    internal static Dictionary<string, string> ReadFolder(Assembly assembly, string prefix, Func<string, bool>? include = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(prefix);
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            string name = resourceName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string remainder = name[prefix.Length..];
            if (include is not null && !include(remainder))
            {
                continue;
            }

            files[remainder] = ReadText(assembly, resourceName);
        }

        return files;
    }
}
