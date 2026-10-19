using Agency.Huddle.SkillPacks.Resolving;
using MudBlazor;

namespace Agency.Huddle.App.Skills.Ui;

/// <summary>How one state looks: the chip's words, its colour family, its leading icon and the one sentence that explains it.</summary>
/// <param name="Text">The chip text; <c>In use</c> for <see cref="ItemState.InUse"/>, which <see cref="SkillStateText.ChipText"/> counts.</param>
/// <param name="Color">The colour the chip tints with.</param>
/// <param name="Icon">The leading icon, so colour is never the only signal.</param>
/// <param name="Sentence">The explanation, with <c>{count}</c>, <c>{holders}</c> and <c>{reason}</c> where they apply.</param>
internal sealed record StateLook(string Text, Color Color, string Icon, string Sentence);

/// <summary>The only place that maps a Skill, Pack or catalog state to a look (Skill Packs spec §6.9.4): the sibling of
/// <c>TaskColors</c> and <c>StatusColors</c>. UI text, not model-facing text.</summary>
internal static class SkillStateText
{
    /// <summary>The look of the <b>Hidden</b> flag chip.</summary>
    public static StateLook Hidden { get; } = new(
        "Hidden",
        Color.Default,
        Icons.Material.Filled.VisibilityOff,
        "You hid it, so it is left out of lists. Teammates who have it keep it. Unhide brings it back.");

    /// <summary>The look of the existing <b>modified locally</b> chip.</summary>
    public static StateLook ModifiedLocally { get; } = new(
        "modified locally",
        Color.Warning,
        Icons.Material.Filled.Edit,
        "Someone edited this Skill's copy on this computer. Applying an update replaces those edits.");

    /// <summary>The look of a state.</summary>
    public static StateLook For(ItemState state)
    {
        return state switch
        {
            ItemState.New => new("New", Color.Info, Icons.Material.Filled.FiberNew,
                "Its row appeared since you last opened this tab. It is usable already; the chip only draws your eye."),
            ItemState.Available => new("Available", Color.Success, Icons.Material.Filled.CheckCircle,
                "It passes Huddle's checks and no Teammate has it yet. You can give it to a Teammate now."),
            ItemState.InUse => new("In use", Color.Primary, Icons.Material.Filled.Group,
                "{count} carry it: {holders}. They use a copy in Skills/, never your AI tool's clone."),
            ItemState.UpdateAvailable => new("Update available", Color.Warning, Icons.Material.Filled.SystemUpdateAlt,
                "The files your AI tool has now differ from the copies Teammates use. Nothing changes until you apply the update."),
            ItemState.NotDownloaded => new("Not downloaded", Color.Default, Icons.Material.Filled.CloudOff,
                "Huddle knows this exists, but there is no folder for it on this computer. Copy a prompt and ask your AI tool to fetch it."),
            ItemState.Deprecated => new("Deprecated", Color.Default, Icons.Material.Filled.NotInterested,
                "Its Source still lists it but advises against it: {reason}. You can still choose it."),
            ItemState.Withdrawn => new("Withdrawn", Color.Error, Icons.Material.Filled.Block,
                "Its Source pulled it: {reason}. New hires cannot take it. Teammates who have it keep working and are flagged."),
            ItemState.Broken => new("Broken", Color.Error, Icons.Material.Filled.Error,
                "A check failed or a pointer leads nowhere. The card names the check and the file. The rest of SKILLS.md still works."),
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state."),
#pragma warning restore HAA0601
        };
    }

    /// <summary>The chip text: <c>In use by N</c> for <see cref="ItemState.InUse"/>, the state's text otherwise.</summary>
    public static string ChipText(ItemState state, int usedBy)
    {
        return state == ItemState.InUse ? InUseText(usedBy) : For(state).Text;
    }

    /// <summary>The text of the <b>In use by N</b> chip.</summary>
    public static string InUseText(int usedBy)
    {
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"In use by {usedBy}");
    }

    /// <summary>The state's sentence with its holders and reason filled in.</summary>
    /// <param name="state">The state.</param>
    /// <param name="holders">The Teammates that hold the item, for <see cref="ItemState.InUse"/>.</param>
    /// <param name="reason">The Source's <c>statusReason</c>, for Deprecated and Withdrawn.</param>
    public static string Sentence(ItemState state, IReadOnlyList<string> holders, string? reason)
    {
        ArgumentNullException.ThrowIfNull(holders);
        string count = holders.Count == 1 ? "1 Teammate" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{holders.Count} Teammates");
        string why = string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason.Trim().TrimEnd('.');
        return For(state).Sentence
            .Replace("{count}", count, StringComparison.Ordinal)
            .Replace("{holders}", AndList(holders), StringComparison.Ordinal)
            .Replace("{reason}", why, StringComparison.Ordinal);
    }

    /// <summary>"Jane", "Jane and Omar", "Jane, Omar and Ravi".</summary>
    public static string AndList(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return names.Count switch
        {
            0 => "nobody",
            1 => names[0],
            _ => JoinAnd(names),
        };
    }

    /// <summary>"a, b and c" for two or more names.</summary>
    private static string JoinAnd(IReadOnlyList<string> names)
    {
        string head = string.Join(", ", names.Take(names.Count - 1));
        return $"{head} and {names[^1]}";
    }
}
