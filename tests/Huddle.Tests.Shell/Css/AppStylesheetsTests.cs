using System.Security.Cryptography;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Css;

/// <summary>
/// Pins <see cref="AppStylesheets.All"/> against the <c>src/Huddle.App/wwwroot/css</c> folder, and (until the
/// consolidation in Phase 2 rewrites the files) pins that the files joined are the bytes of the single
/// <c>app.css</c> they were cut from.
/// </summary>
public sealed class AppStylesheetsTests
{
    /// <summary>
    /// SHA-256 of <c>src/Huddle.App/wwwroot/app.css</c> at commit e932e034 as git stores it, with LF line endings. A
    /// Windows checkout writes CRLF and a Linux one LF, so the test strips every CR before hashing.
    /// </summary>
    private const string OldAppCssSha256 = "9de2915e7ac31dc28b216fd5a4bb99f241c48f247d83f734fe0370e1f4a48690";

    /// <summary>The list equals the <c>.css</c> files in <c>wwwroot/css</c>, in the same order: none is orphaned and none is linked but missing.</summary>
    [Fact]
    public void All_EqualsTheCssFilesOnDisk_InOrder()
    {
        string folder = RepoPaths.App("wwwroot", "css");

        string[] onDisk = [.. Directory.EnumerateFiles(folder, "*.css", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(RepoPaths.App("wwwroot"), file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

        Assert.Equal(onDisk, AppStylesheets.All);
        Assert.NotEmpty(AppStylesheets.All);
        Assert.All(AppStylesheets.All, path => Assert.Equal(AppStylesheets.Folder, path.Split('/')[0]));
    }

    /// <summary>
    /// TEMPORARY, deleted by Phase 2 of the CSS consolidation: the files joined in <see cref="AppStylesheets.All"/> order
    /// are byte for byte the old <c>app.css</c>, so the split changed no byte and no rule moved relative to another.
    /// </summary>
    [Fact]
    public void AppStylesheets_JoinedInOrder_AreTheOldAppCssBytes()
    {
        List<byte> joined = [];
        foreach (string path in CssText.AppCssPaths())
        {
            joined.AddRange(File.ReadAllBytes(path).Where(static value => value != (byte)'\r'));
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData([.. joined]));

        Assert.Equal(OldAppCssSha256, hash);
    }
}
