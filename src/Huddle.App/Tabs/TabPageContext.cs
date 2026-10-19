namespace Agency.Huddle.App.Tabs;

/// <summary>
/// What a page needs to know about the Tab that shows it. The browser URL names only the focused
/// group's Tab, so a page in another group must not read <c>NavigationManager.Uri</c> to learn where it
/// is; the group's page host cascades this instead.
/// </summary>
/// <param name="Url">The site-relative address the Tab shows, including its query.</param>
/// <param name="TabId">The id of the Tab showing the page, which is how the Right sidebar knows whose content to show.</param>
public sealed record TabPageContext(string Url, string TabId)
{
    /// <summary>The path of <see cref="Url"/>, without the query.</summary>
    public string Path
    {
        get
        {
            int query = this.Url.IndexOf('?', StringComparison.Ordinal);
            return query >= 0 ? this.Url[..query] : this.Url;
        }
    }
}
