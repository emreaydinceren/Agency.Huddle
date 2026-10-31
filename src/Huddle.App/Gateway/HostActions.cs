using System.ComponentModel;
using System.Diagnostics;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.App.Gateway;

/// <summary>The actions that act on the machine Huddle runs on, refused while remote access is configured (Spec 6.15).</summary>
/// <param name="state">Whether remote access is configured.</param>
internal sealed class HostActions(IRemoteAccessState state)
{
    /// <summary>The words a refused action reports.</summary>
    public const string RefusedMessage = "This is not available while remote access is on.";

    /// <summary>Opens a file with the application the operating system registered for it.</summary>
    /// <param name="path">The file.</param>
    /// <returns>Whether it was opened, and why not.</returns>
    public HostActionResult OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (state.IsConfigured)
        {
            return new HostActionResult(false, RefusedMessage);
        }

        try
        {
            // Process.Start returns null, rather than throwing, whenever no new process was started, which is what happens with
            // UseShellExecute when no application is registered for the extension.
            using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return process is null
                ? new HostActionResult(false, "no application is registered for this file type on this machine.")
                : new HostActionResult(true, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return new HostActionResult(false, ex.Message);
        }
    }

    /// <summary>Lists the sub-folders of a folder on this machine, for the folder picker.</summary>
    /// <param name="path">A folder, or <see langword="null"/> for the top level.</param>
    /// <returns>The listing; empty, with <see cref="RefusedMessage"/> as its error, while remote access is configured.</returns>
    public FolderListing ListFolders(string? path) =>
        state.IsConfigured
            ? new FolderListing(null, null, [], false, RefusedMessage)
            : FolderBrowser.List(path);
}
