using MudBlazor;

namespace Agency.Huddle.App.Components.Teams;

/// <summary>How a team type is drawn on a card: its icon and the palette role that tints the icon tile.</summary>
/// <param name="Icon">A MudBlazor icon path.</param>
/// <param name="Color">The palette role; the stylesheet turns it into the tile's tint, so a Theme change recolours every tile.</param>
internal sealed record TeamTypeStyle(string Icon, Color Color);

/// <summary>Pure mapping from a team type id to its <see cref="TeamTypeStyle"/>, and to the one-line tagline of its card.</summary>
internal static class TeamTypeStyles
{
    private static readonly Dictionary<string, TeamTypeStyle> Known = new(StringComparer.Ordinal)
    {
        ["development"] = new(Icons.Material.Outlined.Code, Color.Primary),
        ["data"] = new(Icons.Material.Outlined.Storage, Color.Info),
        ["research"] = new(Icons.Material.Outlined.Search, Color.Success),
        ["product"] = new(Icons.Material.Outlined.Layers, Color.Secondary),
        ["life-admin"] = new(Icons.Material.Outlined.Home, Color.Warning),
        ["legal"] = new(Icons.Material.Outlined.Gavel, Color.Error),
        ["support-desk"] = new(Icons.Material.Outlined.Headset, Color.Success),
        ["learning"] = new(Icons.Material.Outlined.School, Color.Tertiary),
        ["small-business"] = new(Icons.Material.Outlined.Storefront, Color.Info),
        ["content"] = new(Icons.Material.Outlined.Article, Color.Secondary),
        ["ops"] = new(Icons.Material.Outlined.Build, Color.Error),
        ["events"] = new(Icons.Material.Outlined.Event, Color.Tertiary),
        ["job-search"] = new(Icons.Material.Outlined.Work, Color.Info),
        ["sales-outreach"] = new(Icons.Material.Outlined.Campaign, Color.Primary),
        ["docs-team"] = new(Icons.Material.Outlined.Description, Color.Secondary),
        ["oss-maintainers"] = new(Icons.Material.Outlined.Public, Color.Success),
        ["grant-proposal"] = new(Icons.Material.Outlined.RequestQuote, Color.Warning),
        ["tax-season"] = new(Icons.Material.Outlined.Receipt, Color.Error),
        ["founders-office"] = new(Icons.Material.Outlined.Business, Color.Primary),
        ["home-project"] = new(Icons.Material.Outlined.Construction, Color.Warning),
        ["book-project"] = new(Icons.Material.Outlined.MenuBook, Color.Tertiary),
        ["caregiving"] = new(Icons.Material.Outlined.Favorite, Color.Error),
        ["community-org"] = new(Icons.Material.Outlined.Diversity3, Color.Success),
        ["relocation"] = new(Icons.Material.Outlined.LocalShipping, Color.Info),
        ["home-lab"] = new(Icons.Material.Outlined.Dns, Color.Primary),
    };

    private static readonly Color[] FallbackColors = [Color.Primary, Color.Info, Color.Success, Color.Secondary, Color.Warning, Color.Tertiary];

    /// <summary>The style of a team type; a user-defined type the table does not know gets the Team icon and a colour fixed by its id.</summary>
    /// <param name="typeId">The team type id.</param>
    /// <returns>The icon and palette role.</returns>
    public static TeamTypeStyle For(string typeId)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        if (Known.TryGetValue(typeId, out TeamTypeStyle? style))
        {
            return style;
        }

        int sum = 0;
        foreach (char c in typeId)
        {
            sum += c;
        }

        return new TeamTypeStyle(Icons.Material.Outlined.Groups, FallbackColors[sum % FallbackColors.Length]);
    }

    /// <summary>The first sentence of a team type's description, without its closing full stop; the whole text when it has only one.</summary>
    /// <param name="description">The description from the hiring pool, or <see langword="null"/>.</param>
    /// <returns>The tagline, or an empty string when there is no description.</returns>
    public static string Tagline(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        string text = description.Trim();
        int end = text.IndexOf(". ", StringComparison.Ordinal);
        string sentence = end < 0 ? text : text[..end];
        return sentence.TrimEnd('.');
    }
}
