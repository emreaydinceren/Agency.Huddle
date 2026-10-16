using MudBlazor;

namespace Agency.Huddle.App.Components.Teams;

/// <summary>How a team type is drawn on a card: its icon. Every type shares one quiet tile; only a selected card tints it.</summary>
/// <param name="Icon">A MudBlazor icon path.</param>
internal sealed record TeamTypeStyle(string Icon);

/// <summary>Pure mapping from a team type id to its <see cref="TeamTypeStyle"/>, and to the one-line tagline of its card.</summary>
internal static class TeamTypeStyles
{
    private static readonly Dictionary<string, TeamTypeStyle> Known = new(StringComparer.Ordinal)
    {
        ["blank"] = new(Icons.Material.Outlined.NoteAdd),
        ["development"] =new(Icons.Material.Outlined.Code),
        ["data"] = new(Icons.Material.Outlined.Storage),
        ["research"] = new(Icons.Material.Outlined.Search),
        ["product"] = new(Icons.Material.Outlined.Layers),
        ["life-admin"] = new(Icons.Material.Outlined.Home),
        ["legal"] = new(Icons.Material.Outlined.Gavel),
        ["support-desk"] = new(Icons.Material.Outlined.Headset),
        ["learning"] = new(Icons.Material.Outlined.School),
        ["small-business"] = new(Icons.Material.Outlined.Storefront),
        ["content"] = new(Icons.Material.Outlined.Article),
        ["ops"] = new(Icons.Material.Outlined.Build),
        ["events"] = new(Icons.Material.Outlined.Event),
        ["job-search"] = new(Icons.Material.Outlined.Work),
        ["sales-outreach"] = new(Icons.Material.Outlined.Campaign),
        ["docs-team"] = new(Icons.Material.Outlined.Description),
        ["oss-maintainers"] = new(Icons.Material.Outlined.Public),
        ["grant-proposal"] = new(Icons.Material.Outlined.RequestQuote),
        ["tax-season"] = new(Icons.Material.Outlined.Receipt),
        ["founders-office"] = new(Icons.Material.Outlined.Business),
        ["home-project"] = new(Icons.Material.Outlined.Construction),
        ["book-project"] = new(Icons.Material.Outlined.MenuBook),
        ["caregiving"] = new(Icons.Material.Outlined.Favorite),
        ["community-org"] = new(Icons.Material.Outlined.Diversity3),
        ["relocation"] = new(Icons.Material.Outlined.LocalShipping),
        ["home-lab"] = new(Icons.Material.Outlined.Dns),
    };

    /// <summary>The style of a team type; a user-defined type the table does not know gets the Team icon.</summary>
    /// <param name="typeId">The team type id.</param>
    /// <returns>The icon.</returns>
    public static TeamTypeStyle For(string typeId)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        if (Known.TryGetValue(typeId, out TeamTypeStyle? style))
        {
            return style;
        }

        return new TeamTypeStyle(Icons.Material.Outlined.Groups);
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
