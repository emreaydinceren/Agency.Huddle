using System.Diagnostics;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins <see cref="LibraryPath.ReadOnly"/> as the resolver sets it (Skill Packs spec §6.11.1, §6.11.3): from the
/// resolved location, so a pinned root or a link that reaches into the Skill Sources folder is read only too.</summary>
public sealed class LibraryPathResolverReadOnlyTests
{
    /// <summary>The root itself and a file in it resolve read only; a Teams path does not.</summary>
    [Fact]
    public void TryResolve_InSkillSourcesRoot_IsReadOnly_ElsewhereIsNot()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string folder = Path.Combine(fixture.DataDir, "SkillSources");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILLS.md"), "# Skills\n");
        Directory.CreateDirectory(Path.Combine(fixture.DataDir, "Teams", "Marketing"));

        Assert.True(fixture.Resolver.TryResolve(LibraryRootStore.SkillSourcesRootId, string.Empty, out LibraryPath? root, out _));
        Assert.True(fixture.Resolver.TryResolve(LibraryRootStore.SkillSourcesRootId, "SKILLS.md", out LibraryPath? file, out _));
        LibraryPath teams = fixture.ResolveTeams("Marketing");

        Assert.True(root.ReadOnly);
        Assert.True(file.ReadOnly);
        Assert.False(teams.ReadOnly);
    }

    /// <summary>A pinned root that holds the Skill Sources folder: a path inside the folder is read only, a path beside it is not.</summary>
    [Fact]
    public void TryResolve_PinnedRootOverlappingTheFolder_IsReadOnlyInsideOnly()
    {
        using LibraryFileServiceFixture fixture = BuildWithSkillSourcesInVault(out string vault);

        LibraryPath inside = fixture.Resolve(vault, "SkillSources/SKILLS.md");
        LibraryPath beside = fixture.Resolve(vault, "note.md");

        Assert.Equal("vault", inside.Root.Id);
        Assert.True(inside.ReadOnly);
        Assert.False(beside.ReadOnly);
    }

    /// <summary>A junction in a writable root that points into the Skill Sources folder resolves read only, through
    /// <c>TryResolve</c> and through <c>TryResolveChild</c>.</summary>
    [Fact]
    public void TryResolve_JunctionIntoTheFolder_IsReadOnly()
    {
        using LibraryFileServiceFixture fixture = BuildWithSkillSourcesInVault(out string vault, supportsReparsePoints: true);
        string link = Path.Combine(vault, "link");
        try
        {
            CreateLink(link, Path.Combine(vault, "SkillSources"));
            fixture.Reload();

            LibraryPath viaLink = fixture.Resolve(vault, "link/SKILLS.md");
            LibraryPath vaultRoot = fixture.Resolve(vault, string.Empty);
            Assert.True(fixture.Resolver.TryResolveChild(vaultRoot, new DirectoryInfo(link), out LibraryPath? child, out _));

            Assert.True(viaLink.ReadOnly);
            Assert.True(child.ReadOnly);
            Assert.False(vaultRoot.ReadOnly);
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link, recursive: false);
            }
        }
    }

    /// <summary>A fixture whose Skill Sources folder is <c>Vault/SkillSources</c>, with <c>Vault</c> pinned.</summary>
    private static LibraryFileServiceFixture BuildWithSkillSourcesInVault(out string vault, bool supportsReparsePoints = false)
    {
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(
            options => options.SkillSourcesDir = Path.Combine(options.DataDir, "Vault", "SkillSources"),
            supportsReparsePoints);
        string folder = Path.Combine(fixture.DataDir, "Vault", "SkillSources");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILLS.md"), "# Skills\n");
        vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "note\n");
        return fixture;
    }

    /// <summary>A junction on Windows (no elevation needed), a symbolic link elsewhere.</summary>
    private static void CreateLink(string link, string target)
    {
        if (OperatingSystem.IsWindows())
        {
            ProcessStartInfo startInfo = new("cmd")
            {
                ArgumentList = { "/c", "mklink", "/J", link, target },
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("mklink did not start.");
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(link, target);
        }
    }
}
