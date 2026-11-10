using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Css;

/// <summary>Pins <see cref="AppStylesheets.All"/> against the <c>src/Huddle.App/wwwroot/css</c> folder.</summary>
public sealed class AppStylesheetsTests
{
    /// <summary>The list holds exactly the <c>.css</c> files in <c>wwwroot/css</c>, each once: none is orphaned and none is linked but missing. The files are named for their feature, so the link order is the list's own and is not compared with the name order.</summary>
    [Fact]
    public void All_HoldsTheCssFilesOnDisk_EachOnce()
    {
        string folder = RepoPaths.App("wwwroot", "css");

        string[] onDisk = [.. Directory.EnumerateFiles(folder, "*.css", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(RepoPaths.App("wwwroot"), file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

        Assert.Equal(onDisk, AppStylesheets.All.Order(StringComparer.Ordinal));
        Assert.NotEmpty(AppStylesheets.All);
        Assert.All(AppStylesheets.All, path => Assert.Equal(AppStylesheets.Folder, path.Split('/')[0]));
    }
}
