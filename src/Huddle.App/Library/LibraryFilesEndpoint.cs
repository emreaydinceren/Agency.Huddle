using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The <c>/library-files/{rootId}/{**path}</c> endpoint (Spec §6.11, §9 item 4; Task 12.8): the
/// stated exception to rules.md's rule that a file under <c>{DataDir}</c> is served by
/// <c>UseStaticFiles</c> + <c>PhysicalFileProvider</c>. A <c>PhysicalFileProvider</c> is rooted at
/// construction time, but a pinned Library root can point anywhere on disk and can change while the
/// app is running (Spec §7), so no single provider can serve every Library root. This handler
/// re-runs <see cref="LibraryPathResolver.TryResolve"/> on every request instead (Spec §9 item 4),
/// and serves only bytes it has itself confirmed are an image by magic bytes - never an SVG, and
/// never by trusting the request's extension.
/// </summary>
internal static class LibraryFilesEndpoint
{
    /// <summary>The most of a file read up front to detect its kind (Spec §6.11; matches <see cref="LibraryFileService"/>'s own head size).</summary>
    private const int DetectionHeadBytes = 8192;

    /// <summary>
    /// Handles one request: 404s on a disabled Library, an unresolvable path, a path through a
    /// hidden folder, a missing file, or a file whose head is not one of the four accepted image
    /// signatures; otherwise serves the file with <c>X-Content-Type-Options: nosniff</c>.
    /// </summary>
    /// <param name="rootId">The Library Root id from the route.</param>
    /// <param name="path">The root-relative path from the route's catch-all segment.</param>
    /// <param name="httpContext">The current request, for setting <c>X-Content-Type-Options</c> on a served image.</param>
    /// <param name="resolver">Resolves the (rootId, path) pair to a path inside the Library.</param>
    /// <param name="options">The bound <see cref="TeamOptions"/>, for <see cref="LibraryOptions.Enabled"/> and the hidden-folder ignore list.</param>
    /// <param name="loggerFactory">Creates the endpoint's logger (a static class can't be a generic <c>ILogger</c> argument, CS0718), which logs an unexpected file-system failure while reading the head.</param>
    /// <param name="ct">Cancels the head read.</param>
    /// <returns>A file result on success, a 404 result otherwise.</returns>
    internal static async Task<IResult> HandleAsync(
        string rootId,
        string path,
        HttpContext httpContext,
        LibraryPathResolver resolver,
        IOptions<TeamOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rootId);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (!options.Value.Library.Enabled)
        {
            return Results.NotFound();
        }

        if (!resolver.TryResolve(rootId, path, out LibraryPath? resolved, out string? _))
        {
            return Results.NotFound();
        }

        IReadOnlyList<string> ignoredFolders = options.Value.FileChanges.EffectiveIgnore;
        bool hideUnderscoreFolders = resolved.Root.Kind == LibraryRootKind.Teams;
        foreach (string segment in resolved.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (LibraryHiddenFolders.IsHidden(segment, ignoredFolders, hideUnderscoreFolders))
            {
                return Results.NotFound();
            }
        }

        if (!File.Exists(resolved.FullPath))
        {
            return Results.NotFound();
        }

        byte[] head;
        try
        {
            head = await ReadHeadAsync(resolved.FullPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger(typeof(LibraryFilesEndpoint).FullName ?? nameof(LibraryFilesEndpoint)).LogWarning(ex, "Library file '{FullPath}' could not be read for serving.", resolved.FullPath);
            return Results.NotFound();
        }

        string? contentType = LibraryFileKinds.ImageContentType(head);
        if (contentType is null)
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(resolved.FullPath, contentType, fileDownloadName: null, enableRangeProcessing: false);
    }

    /// <summary>Reads the first <see cref="DetectionHeadBytes"/> bytes of <paramref name="fullPath"/> (or the whole file when shorter).</summary>
    /// <param name="fullPath">The file to read.</param>
    /// <param name="ct">Cancels the read.</param>
    private static async Task<byte[]> ReadHeadAsync(string fullPath, CancellationToken ct)
    {
        using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int length = (int)Math.Min(stream.Length, DetectionHeadBytes);
        byte[] buffer = new byte[length];
        int totalRead = 0;
        while (totalRead < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead), ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead == length ? buffer : buffer[..totalRead];
    }
}
