namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Source-text guards for UX-R15 (plan U18 T18.3): Add folder in the Library sidebar validates and pins through
/// <c>LibraryRootStore.TryPin</c> and shows <c>LibraryRootStore.TrustWarning</c>, rather than repeating the
/// directory check and the warning text that Settings > Library already shares.
/// </summary>
public sealed class LibraryAddFolderSourceTests
{
    /// <summary>The nav link pins through <c>TryPin</c> and does not repeat the existence check or the Save call.</summary>
    [Fact]
    public void LibraryNavLink_PinsThroughTryPin()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryNavLink.razor"));

        Assert.Contains("this.Roots.TryPin(", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("Directory.Exists", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("this.Roots.Save(", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }

    /// <summary>The nav link shows the shared warning constant, not a second copy of its text.</summary>
    [Fact]
    public void LibraryNavLink_ShowsTheSharedTrustWarning()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryNavLink.razor"));

        Assert.Contains("LibraryRootStore.TrustWarning", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain("Anything in this folder", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }
}
