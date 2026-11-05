using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Agency.Huddle.Tests.Files;

/// <summary>
/// Denies the current user access to a file or folder until disposed, so that opening or listing it throws
/// <see cref="UnauthorizedAccessException"/>: what Windows reports for a path that is being deleted, which a test cannot time.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DeniedAccess : IDisposable
{
    private readonly Action restore;

    private DeniedAccess(Action restore) => this.restore = restore;

    /// <summary>Denies listing a folder.</summary>
    /// <param name="path">The folder.</param>
    /// <returns>The denial; dispose it to lift the rule.</returns>
    public static DeniedAccess ToFolder(string path)
    {
        DirectoryInfo folder = new(path);
        FileSystemAccessRule rule = Deny(FileSystemRights.ListDirectory);
        DirectorySecurity security = folder.GetAccessControl();
        security.AddAccessRule(rule);
        folder.SetAccessControl(security);
        return new DeniedAccess(() =>
        {
            DirectorySecurity current = folder.GetAccessControl();
            _ = current.RemoveAccessRule(rule);
            folder.SetAccessControl(current);
        });
    }

    /// <summary>Denies reading a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The denial; dispose it to lift the rule.</returns>
    public static DeniedAccess ToFile(string path)
    {
        FileInfo file = new(path);
        FileSystemAccessRule rule = Deny(FileSystemRights.ReadData);
        FileSecurity security = file.GetAccessControl();
        security.AddAccessRule(rule);
        file.SetAccessControl(security);
        return new DeniedAccess(() =>
        {
            FileSecurity current = file.GetAccessControl();
            _ = current.RemoveAccessRule(rule);
            file.SetAccessControl(current);
        });
    }

    /// <summary>Lifts the rule, so the path can be read and deleted again.</summary>
    public void Dispose() => this.restore();

    private static FileSystemAccessRule Deny(FileSystemRights rights) =>
        new(WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("The current user has no SID."), rights, AccessControlType.Deny);
}
