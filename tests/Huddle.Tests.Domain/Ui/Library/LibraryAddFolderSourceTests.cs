namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Source-text guards for UX-R15 (plan U18 T18.3): Add folder, now only in Settings > Library (the sidebar links to
/// the Library once and no longer carries its own Add row), validates and pins through
/// <c>LibraryRootStore.TryPin</c> and shows <c>LibraryRootStore.TrustWarning</c>, rather than repeating the
/// directory check and the warning text.
/// </summary>
public sealed class LibraryAddFolderSourceTests
{
    /// <summary>The Settings panel pins through <c>TryPin</c> and does not repeat the existence check.</summary>
    [Fact]
    public void LibraryPanel_PinsThroughTryPin()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Settings", "LibraryPanel.razor"));

        Assert.Contains("this.Roots.TryPin(", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("Directory.Exists", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }

    /// <summary>The Settings panel shows the shared warning constant, not a second copy of its text.</summary>
    [Fact]
    public void LibraryPanel_ShowsTheSharedTrustWarning()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Settings", "LibraryPanel.razor"));

        Assert.Contains("LibraryRootStore.TrustWarning", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("Anything in this folder", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }
}
