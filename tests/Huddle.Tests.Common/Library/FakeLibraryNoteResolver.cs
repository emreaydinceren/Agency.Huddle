using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// A delegate-backed fake of <see cref="ILibraryNoteResolver"/>, configurable per test through the three
/// optional callbacks (default: every method returns <see langword="null"/>). Shared by
/// <see cref="Agency.Huddle.Tests.Services.MarkdownRendererTests"/> (Task 9.2) and reusable as-is by 9.1's
/// chat-path tests and 9.4's note-view tests, since it also implements the base
/// <see cref="ILibraryReferenceResolver"/> members they need.
/// </summary>
internal sealed class FakeLibraryNoteResolver : ILibraryNoteResolver
{
    private readonly Func<string, LibraryReference?> resolvePath;
    private readonly Func<LibraryPath, WikiLink, LibraryReference?> resolveWikiLink;
    private readonly Func<LibraryPath, string, LibraryReference?> resolveRelative;

    /// <summary>Creates a fake wired to the given callbacks; any omitted callback always returns <see langword="null"/>.</summary>
    /// <param name="resolvePath">Answers <see cref="ILibraryReferenceResolver.ResolvePath"/>.</param>
    /// <param name="resolveWikiLink">Answers <see cref="ILibraryReferenceResolver.ResolveWikiLink"/>.</param>
    /// <param name="resolveRelative">Answers <see cref="ILibraryNoteResolver.ResolveRelative"/>.</param>
    public FakeLibraryNoteResolver(
        Func<string, LibraryReference?>? resolvePath = null,
        Func<LibraryPath, WikiLink, LibraryReference?>? resolveWikiLink = null,
        Func<LibraryPath, string, LibraryReference?>? resolveRelative = null)
    {
        this.resolvePath = resolvePath ?? (_ => null);
        this.resolveWikiLink = resolveWikiLink ?? ((_, _) => null);
        this.resolveRelative = resolveRelative ?? ((_, _) => null);
    }

    /// <inheritdoc/>
    public LibraryReference? ResolvePath(string absolutePath) => this.resolvePath(absolutePath);

    /// <inheritdoc/>
    public LibraryReference? ResolveWikiLink(LibraryPath from, WikiLink link) => this.resolveWikiLink(from, link);

    /// <inheritdoc/>
    public LibraryReference? ResolveRelative(LibraryPath from, string url) => this.resolveRelative(from, url);
}
