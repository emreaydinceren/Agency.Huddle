using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="WindowsRecycleBin"/> and its registration (Spec §6.4, Task 0.2's facts,
/// corrections-B4 items 29-31).
/// </summary>
public sealed class WindowsRecycleBinTests
{
    /// <summary>On Windows, <see cref="ServiceCollectionExtensions.AddTeamServices"/> registers the real
    /// <see cref="WindowsRecycleBin"/> as <see cref="IRecycleBin"/> (corrections-B4 item 31).</summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void Registration_OnWindows_IsWindowsRecycleBin()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only registration.");
            return;
        }

        using TempDataDir dir = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dir.Path })
            .Build();
        ServiceCollection services = new();

        services.AddTeamServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        IRecycleBin recycleBin = provider.GetRequiredService<IRecycleBin>();

        Assert.IsType<WindowsRecycleBin>(recycleBin);
    }

    /// <summary>Off Windows, <see cref="ServiceCollectionExtensions.AddTeamServices"/> registers
    /// <see cref="NotAvailableRecycleBin"/>, never <see cref="WindowsRecycleBin"/> (corrections-B4 item 31).</summary>
    [Fact]
    public void Registration_Elsewhere_IsUnavailable()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Non-Windows-only registration.");
            return;
        }

        using TempDataDir dir = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dir.Path })
            .Build();
        ServiceCollection services = new();

        services.AddTeamServices(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        IRecycleBin recycleBin = provider.GetRequiredService<IRecycleBin>();

        Assert.IsType<NotAvailableRecycleBin>(recycleBin);
    }

    /// <summary>Really sends a temp file to the Windows recycle bin and removes it from its folder
    /// (Task 0.2: on an existing local file, the file is gone from its folder immediately). Explicit
    /// because it touches the real shell recycle bin.</summary>
    [Fact(Explicit = true)]
    [SupportedOSPlatform("windows")]
    public void Send_File_RemovesFromFolder()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only: recycles through the real shell.");
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"windows-recycle-bin-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "hello");
        WindowsRecycleBin recycleBin = new();

        bool sent = recycleBin.TrySend(path, out string? error);

        Assert.True(sent);
        Assert.Null(error);
        Assert.False(File.Exists(path));
    }

    /// <summary>Only a <see cref="DriveType.Fixed"/> local path is recyclable (Task 0.2's E-8 risk):
    /// every other drive type, and a UNC path even on a fixed drive, is refused.</summary>
    [Theory]
    [InlineData(DriveType.Fixed, @"C:\Users\someone\note.md", true)]
    [InlineData(DriveType.Network, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.Removable, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.CDRom, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.Ram, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.Unknown, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.NoRootDirectory, @"C:\Users\someone\note.md", false)]
    [InlineData(DriveType.Fixed, @"\\server\share\x.md", false)]
    [InlineData(DriveType.Fixed, "//server/share/x.md", false)]
    public void IsRecyclable_DriveTypeAndPathShape(DriveType type, string realPath, bool expected)
    {
        bool actual = WindowsRecycleBin.IsRecyclable(realPath, type);

        Assert.Equal(expected, actual);
    }

    /// <summary>A path under a directory link resolves to the link's real, final target (corrections-B4
    /// item 30, B3 item 26): <see cref="WindowsRecycleBin.RealPath"/> follows the link rather than
    /// reporting the lexical path the caller passed in.</summary>
    [Fact]
    public void RealPath_PathUnderDirectoryLink_ResolvesFinalTarget()
    {
        using TempDataDir dir = new();
        string targetDir = Path.Combine(dir.Path, "target");
        Directory.CreateDirectory(targetDir);
        string filePath = Path.Combine(targetDir, "note.md");
        File.WriteAllText(filePath, "hello");
        string linkDir = Path.Combine(dir.Path, "link");

        if (!TestLinks.TryCreateLink(linkDir, targetDir))
        {
            Assert.Skip("Could not create a directory link on this machine.");
            return;
        }

        try
        {
            string linkedPath = Path.Combine(linkDir, "note.md");

            string realPath = WindowsRecycleBin.RealPath(linkedPath);

            Assert.Equal(filePath, realPath, FolderSnapshot.PathComparer);
        }
        finally
        {
            TestLinks.RemoveLink(linkDir);
        }
    }

    /// <summary>The allowed neighbour of <see cref="RealPath_PathUnderDirectoryLink_ResolvesFinalTarget"/>:
    /// a plain path with no link in it resolves to itself, normalised.</summary>
    [Fact]
    public void RealPath_PlainPath_ReturnsItselfNormalized()
    {
        using TempDataDir dir = new();
        string filePath = Path.Combine(dir.Path, "note.md");
        File.WriteAllText(filePath, "hello");

        string realPath = WindowsRecycleBin.RealPath(filePath);

        Assert.Equal(Path.GetFullPath(filePath), realPath, FolderSnapshot.PathComparer);
    }

    /// <summary>The E-8 permanent-delete risk (Task 0.2): <see cref="WindowsRecycleBin.TrySend"/> checks
    /// the resolved path's drive type BEFORE ever calling into the shell, so an unrecyclable drive is
    /// refused and the file is left untouched. Uses the internal <c>driveTypeOf</c> constructor so the
    /// real shell is never invoked; safe to run by default.</summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void TrySend_NetworkDrive_RefusesWithoutTouchingShell()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("WindowsRecycleBin is Windows-only.");
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"windows-recycle-bin-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "hello");
        try
        {
            WindowsRecycleBin recycleBin = new(_ => DriveType.Network);

            bool sent = recycleBin.TrySend(path, out string? error);

            Assert.False(sent);
            Assert.Equal(WindowsRecycleBin.UnavailableReason, error);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The UNC check runs LEXICALLY, before any other step (a <c>\\server</c> lookup can hang
    /// on SMB timeouts): the injected <c>driveTypeOf</c> throws if it is ever called, so this fails
    /// unless the refusal happens before the drive-type lookup (corrections-B4 item 30).</summary>
    [Fact]
    [SupportedOSPlatform("windows")]
    public void TrySend_UncPath_RefusesWithoutTouchingShell()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("WindowsRecycleBin is Windows-only.");
            return;
        }

        WindowsRecycleBin recycleBin = new(_ => throw new InvalidOperationException("must not be called for a UNC path"));

        bool sent = recycleBin.TrySend(@"\\server\share\note.md", out string? error);

        Assert.False(sent);
        Assert.Equal(WindowsRecycleBin.UnavailableReason, error);
    }

    /// <summary>An <see cref="IOException"/> from the underlying send maps to the settled "in use"
    /// refusal (corrections-B4 item 31), naming the item.</summary>
    [Fact]
    public void RefusalFor_IOException_NamesItemInUse()
    {
        IOException exception = new("locked");

        string refusal = WindowsRecycleBin.RefusalFor(exception, "note.md");

        Assert.Equal("Couldn't delete note.md: something inside it is in use.", refusal);
    }

    /// <summary>An <see cref="OperationCanceledException"/> from the underlying send maps to the settled,
    /// unqualified refusal (corrections-B4 item 31), naming the item.</summary>
    [Fact]
    public void RefusalFor_OperationCanceledException_NamesItem()
    {
        OperationCanceledException exception = new();

        string refusal = WindowsRecycleBin.RefusalFor(exception, "note.md");

        Assert.Equal("Couldn't delete note.md.", refusal);
    }

    /// <summary>An <see cref="UnauthorizedAccessException"/> from the underlying send (an ACL-denied file
    /// or folder) maps to the settled, unqualified refusal (corrections-B4 item 31), naming the item.</summary>
    [Fact]
    public void RefusalFor_UnauthorizedAccessException_NamesItem()
    {
        UnauthorizedAccessException exception = new();

        string refusal = WindowsRecycleBin.RefusalFor(exception, "note.md");

        Assert.Equal("Couldn't delete note.md.", refusal);
    }

}
