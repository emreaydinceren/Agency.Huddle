using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Keeps the skin token registry honest from the other side: <c>AppCss_ReadsOnlyKnownTokens</c> refuses a
/// <c>var(--hu-*)</c> the registry does not have, and this refuses a registered token that no stylesheet reads, which a
/// skin could set without anything on screen changing.
/// </summary>
public sealed class SkinTokenUsageTests
{
    /// <summary>Every registered token is read as <c>var(--hu-…)</c> by app.css, app-vars.css or a scoped stylesheet.</summary>
    [Fact]
    public void SkinTokens_EveryTokenIsRead()
    {
        string root = CssSource.RepoPath("src", "Huddle.App");
        string css = string.Join(
            '\n',
            Directory.EnumerateFiles(root, "*.css", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        string[] unread = [.. SkinTokens.All
            .Where(token => !css.Contains($"var({token.CssName})", StringComparison.Ordinal))
            .Select(static token => token.Key)];

        Assert.Empty(unread);
    }
}
