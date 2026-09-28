using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The real <see cref="ILibraryReferenceResolver"/>/<see cref="ILibraryNoteResolver"/> (Spec §6.6, Task 9.3):
/// resolves an absolute path, a wikilink or a relative Markdown link to a <see cref="LibraryReference"/>.
/// <see cref="ResolvePath"/> stays cheap by construction (D9 item 39): <see cref="LibraryPathResolver.TryResolveAbsolute"/>
/// plus one existence check, and it never builds the wikilink index. There is no per-render cache: a cache would
/// keep a stale <c>Exists</c> for a file created after the first render.
/// </summary>
/// <param name="paths">Resolves and classifies paths inside the Library's roots.</param>
/// <param name="index">Resolves wikilinks against a root's cached file list, when available.</param>
/// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="LibraryOptions.Enabled"/>.</param>
internal sealed partial class LibraryReferenceResolver(LibraryPathResolver paths, WikiLinkIndex index, IOptions<TeamOptions> options) : ILibraryNoteResolver
{
    private static readonly char[] FragmentOrQuery = ['#', '?'];

    private readonly LibraryPathResolver paths = paths;
    private readonly WikiLinkIndex index = index;
    private readonly IOptions<TeamOptions> options = options;

    /// <summary>Matches a URI scheme prefix (<c>http:</c>, <c>mailto:</c>, a drive letter like <c>C:</c>): none of
    /// these are a relative Library link, and <see cref="Uri.TryCreate(string, UriKind, out Uri?)"/> with
    /// <see cref="UriKind.Absolute"/> is OS-dependent for a leading <c>/</c> (it parses as <c>file:///…</c> on
    /// Linux but not on Windows), so the scheme is matched explicitly instead (R5 fix card).</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeRegex();

    /// <inheritdoc/>
    public LibraryReference? ResolvePath(string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);

        if (!this.options.Value.Library.Enabled)
        {
            return null;
        }

        if (!this.paths.TryResolveAbsolute(absolutePath, out LibraryPath? resolved))
        {
            return null;
        }

        bool exists = File.Exists(resolved.FullPath) || Directory.Exists(resolved.FullPath);
        return new LibraryReference(resolved.Root.Id, resolved.RelativePath, exists);
    }

    /// <inheritdoc/>
    public LibraryReference? ResolveWikiLink(LibraryPath from, WikiLink link)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(link);

        if (!this.options.Value.Library.Enabled)
        {
            return null;
        }

        WikiLinkResolution resolution = this.index.Resolve(from, link);
        if (resolution.Path is null)
        {
            return new LibraryReference(from.Root.Id, link.Target, Exists: false);
        }

        return new LibraryReference(from.Root.Id, resolution.Path, Exists: true, resolution.IsAmbiguous);
    }

    /// <inheritdoc/>
    public LibraryReference? ResolveRelative(LibraryPath from, string url)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(url);

        if (!this.options.Value.Library.Enabled)
        {
            return null;
        }

        if (url.Length == 0)
        {
            return null;
        }

        if (SchemeRegex().IsMatch(url))
        {
            // A scheme (http:, mailto:, a drive letter such as C:) is not a relative file link (Spec §6.6).
            return null;
        }

        int cut = url.IndexOfAny(FragmentOrQuery);
        string path = cut < 0 ? url : url[..cut];
        if (path.Length == 0)
        {
            // Only a #fragment or ?query, no path (e.g. a bare heading anchor "#heading").
            return null;
        }

        string decoded = Uri.UnescapeDataString(path);

        List<string> stack = [];
        if (!decoded.StartsWith('/'))
        {
            int lastSlash = from.RelativePath.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                stack.AddRange(from.RelativePath[..lastSlash].Split('/'));
            }
        }

        foreach (string segment in decoded.Split('/'))
        {
            if (segment is "." or "")
            {
                continue;
            }

            if (segment == "..")
            {
                if (stack.Count == 0)
                {
                    // Escaping the root refuses the link outright (corrections-B5, item 22 area).
                    return null;
                }

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(segment);
        }

        string relativePath = string.Join('/', stack);
        if (!this.paths.TryResolve(from.Root.Id, relativePath, out LibraryPath? resolved, out _))
        {
            return null;
        }

        bool exists = File.Exists(resolved.FullPath) || Directory.Exists(resolved.FullPath);
        return new LibraryReference(resolved.Root.Id, resolved.RelativePath, exists);
    }
}
