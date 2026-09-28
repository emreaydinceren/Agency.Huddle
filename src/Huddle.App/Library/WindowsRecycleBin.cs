using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Microsoft.VisualBasic.FileIO;

namespace Agency.Huddle.App.Library;

/// <summary>
/// The real Windows recycle bin (Spec §6.4, Task 0.2, corrections-B4 items 29-31): sends a file or
/// folder to the shell recycle bin via <see cref="Microsoft.VisualBasic.FileIO.FileSystem"/>, after
/// refusing any path this machine cannot recycle (Spec §10 E-8) - a network or removable drive, or a
/// UNC path - WITHOUT ever calling into the shell for it, and without ever permanently deleting
/// anything on such a path.
/// </summary>
internal sealed class WindowsRecycleBin : IRecycleBin
{
    /// <summary>Settled text (Spec §10 E-8) for a path this machine cannot send to a recycle bin.</summary>
    internal const string UnavailableReason = "Couldn't delete: the Recycle Bin isn't available here.";

    private readonly Func<string, DriveType> driveTypeOf;

    /// <summary>Creates the real recycle bin, reading each path's drive type from <see cref="DriveInfo"/>.</summary>
    public WindowsRecycleBin()
        : this(root => new DriveInfo(root).DriveType)
    {
    }

    /// <summary>
    /// Creates the recycle bin with an injected drive-type lookup, so a test can prove the E-8 refusal
    /// (corrections-B4 item 30) without ever depending on a real network or removable drive being present.
    /// </summary>
    /// <param name="driveTypeOf">Returns the <see cref="DriveType"/> for a path root (e.g. <c>C:\</c>).</param>
    internal WindowsRecycleBin(Func<string, DriveType> driveTypeOf)
    {
        ArgumentNullException.ThrowIfNull(driveTypeOf);
        this.driveTypeOf = driveTypeOf;
    }

    /// <summary>
    /// Sends <paramref name="fullPath"/> to the recycle bin. Checked in order (corrections-B4 items
    /// 30-31): (1) a UNC-shaped path is refused LEXICALLY, before any filesystem call - a
    /// <c>\\server</c> lookup can hang on SMB timeouts; (2) the real, final target is resolved,
    /// following any link; (3) the real target's drive must be <see cref="DriveType.Fixed"/>, or this
    /// refuses with the E-8 text rather than letting the shell silently, permanently delete; (4) the
    /// send itself, via <see cref="Microsoft.VisualBasic.FileIO.FileSystem"/>, catching only
    /// <see cref="IOException"/>, <see cref="OperationCanceledException"/> and
    /// <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    /// <param name="fullPath">The absolute path of the file or folder to recycle.</param>
    /// <param name="error">The refusal reason, when this returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the item was sent to the recycle bin.</returns>
    [SupportedOSPlatform("windows")]
    public bool TrySend(string fullPath, [NotNullWhen(false)] out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        if (IsUncShaped(fullPath))
        {
            error = UnavailableReason;
            return false;
        }

        string real = RealPath(fullPath);
        string? root = Path.GetPathRoot(real);
        DriveType type = root is null ? DriveType.Unknown : this.driveTypeOf(root);
        if (!IsRecyclable(real, type))
        {
            error = UnavailableReason;
            return false;
        }

        try
        {
            if (Directory.Exists(real))
            {
                FileSystem.DeleteDirectory(real, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(real, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }

            error = null;
            return true;
        }
        catch (IOException ex)
        {
            error = RefusalFor(ex, Path.GetFileName(real));
            return false;
        }
        catch (OperationCanceledException ex)
        {
            error = RefusalFor(ex, Path.GetFileName(real));
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = RefusalFor(ex, Path.GetFileName(real));
            return false;
        }
    }

    /// <summary>Only a <see cref="DriveType.Fixed"/> local path is recyclable (Task 0.2's E-8 risk):
    /// every other drive type, and a UNC path even on a fixed drive, is refused.</summary>
    /// <param name="realPath">The already-resolved, final path.</param>
    /// <param name="type">The drive type of <paramref name="realPath"/>'s root.</param>
    /// <returns><see langword="true"/> when this machine can recycle <paramref name="realPath"/>.</returns>
    internal static bool IsRecyclable(string realPath, DriveType type) =>
        type == DriveType.Fixed && !IsUncShaped(realPath);

    /// <summary>Resolves <paramref name="fullPath"/> to its real, final target, following every reparse
    /// point along the way (corrections-B4 item 30, B3 item 26) via <see cref="LinkPaths"/>.</summary>
    /// <param name="fullPath">The lexical path to resolve.</param>
    /// <returns>The final, real path.</returns>
    internal static string RealPath(string fullPath)
    {
        string full = Path.GetFullPath(fullPath);
        string root = Path.GetPathRoot(full) ?? string.Empty;
        string relative = full[root.Length..];
        string current = root;

        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (LinkPaths.ResolveIfLink(current) is string target)
            {
                current = target;
            }
        }

        return current;
    }

    /// <summary>Maps a caught exception from the underlying send to a settled refusal text naming
    /// <paramref name="name"/> (corrections-B4 item 31).</summary>
    /// <param name="exception">The exception caught from the underlying send.</param>
    /// <param name="name">The recycled item's display name.</param>
    /// <returns>The settled refusal text.</returns>
    internal static string RefusalFor(Exception exception, string name) => exception switch
    {
        IOException => $"Couldn't delete {name}: something inside it is in use.",
        _ => $"Couldn't delete {name}.",
    };

    /// <summary>Lexical UNC check (before any filesystem call: an SMB lookup on a dead <c>\\server</c>
    /// can hang), matching both separator styles.</summary>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is UNC-shaped.</returns>
    private static bool IsUncShaped(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
}
