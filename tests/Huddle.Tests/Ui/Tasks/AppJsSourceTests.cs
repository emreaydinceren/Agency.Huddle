namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.9's <c>window.huddleStorage</c> helper and §13.13.1's <c>window.huddleClipboard</c>
/// helper as source facts, because the repo has no JavaScript test runner - the same source-text
/// approach <see cref="AppStylesheetTests"/> and <c>ThemeSourceTests</c> use for <c>app.css</c>.
/// </summary>
public sealed class AppJsSourceTests
{
    /// <summary><c>window.huddleStorage</c> reads through <c>localStorage.getItem</c> inside a try/catch (Spec §13.9).</summary>
    [Fact]
    public void AppJs_DefinesHuddleStorage_WithTryCatch()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        Assert.Contains("window.huddleStorage", text, StringComparison.Ordinal);
        Assert.Contains("localStorage.getItem", text, StringComparison.Ordinal);
        Assert.Contains("catch", text, StringComparison.Ordinal);
    }

    /// <summary><c>window.huddleClipboard</c> checks <c>isSecureContext</c> and falls back to <c>execCommand("copy")</c> (Spec §13.13.1).</summary>
    [Fact]
    public void AppJs_DefinesHuddleClipboard_WithSecureContextCheckAndFallback()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        Assert.Contains("window.huddleClipboard", text, StringComparison.Ordinal);
        Assert.Contains("isSecureContext", text, StringComparison.Ordinal);
        Assert.Contains("execCommand(\"copy\")", text, StringComparison.Ordinal);
    }

    /// <summary>The pre-existing <c>teamComposer</c> and <c>teamScroll</c> helpers stay unchanged (Task 11.2.i).</summary>
    [Fact]
    public void AppJs_KeepsTeamComposerAndTeamScroll()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        Assert.Contains("window.teamComposer", text, StringComparison.Ordinal);
        Assert.Contains("window.teamScroll", text, StringComparison.Ordinal);
    }
}
