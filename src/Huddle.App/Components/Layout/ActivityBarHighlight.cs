namespace Agency.Huddle.App.Components.Layout;

/// <summary>
/// Which Activity bar tile is lit when a page tile (Feed, Archived, Settings) and a Sidebar view tile (or a Team shortcut)
/// could both claim it: the one clicked last. A page tile is lit while its page is the active Tab and it was clicked, or its
/// page brought forward, after the last view tile click; otherwise the shown view's tile is lit. Only the look changes:
/// the Sidebar keeps showing its view either way. MainLayout owns one per circuit.
/// </summary>
internal sealed class ActivityBarHighlight
{
    /// <summary>Whether a page tile was used more recently than a view tile. True at first, so a page opened by address is lit.</summary>
    private bool pageTileLast = true;

    /// <summary>The active Tab address last seen, so a move to a page tile's page lights it once rather than on every change.</summary>
    private string? activeUrl;

    /// <summary>Whether the page tiles wear their active highlight; each still lights only on its own page.</summary>
    public bool PageTileLit => this.pageTileLast;

    /// <summary>Whether the shown view's tile (or Team shortcut) is lit: not while a lit page tile's page is the active Tab.</summary>
    /// <param name="activeUrl">The site-relative address of the focused pane's active Tab, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when the view's tile is the one lit.</returns>
    public bool ViewTileLit(string? activeUrl) => !(this.pageTileLast && SidebarViews.IsPageTileAddress(activeUrl));

    /// <summary>A view tile or Team shortcut was clicked. Collapsing the Sidebar leaves no view to light, so the page tile lights again.</summary>
    /// <param name="collapsed">Whether the click collapsed the Sidebar.</param>
    public void ViewClicked(bool collapsed)
    {
        this.pageTileLast = collapsed;
    }

    /// <summary>A page tile was clicked: it is now the last tile used.</summary>
    public void PageTileClicked()
    {
        this.pageTileLast = true;
    }

    /// <summary>Records the active Tab; when it moved to a page tile's page, that page's tile becomes the last one used.</summary>
    /// <param name="url">The site-relative address of the focused pane's active Tab, or <see langword="null"/> when it has none.</param>
    /// <returns>Whether the active Tab's address changed, which may change which tile is lit.</returns>
    public bool ActiveTabSeen(string? url)
    {
        if (string.Equals(url, this.activeUrl, StringComparison.Ordinal))
        {
            return false;
        }

        this.activeUrl = url;
        if (SidebarViews.IsPageTileAddress(url))
        {
            this.pageTileLast = true;
        }

        return true;
    }
}
