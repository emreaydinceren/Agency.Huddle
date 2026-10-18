using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>The one-line reasons a card shows for a Pack or Skill that is not ready (Skill Packs spec §6.10.2).</summary>
internal static class SkillSetupText
{
    /// <summary>The reason line for a setup item.</summary>
    public static string Reason(SkillSetupItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string noun = item.Kind == SkillSetupKind.Pack ? "Pack" : "Skill";
        string name = item.Kind == SkillSetupKind.Pack && item.Detail.Length > 0 && item.Problem == SkillAvailability.NotDownloaded ? item.Detail : item.Name;
        return item.Problem switch
        {
            SkillAvailability.NotDownloaded => $"{noun} \"{name}\" not downloaded",
            SkillAvailability.Broken => $"{noun} \"{item.Name}\" is broken: {item.Detail}",
            SkillAvailability.Withdrawn => $"{noun} \"{item.Name}\" was withdrawn by its Source: {item.Detail}",
            SkillAvailability.Unknown when item.Kind == SkillSetupKind.Pack => $"Pack \"{item.Name}\" lists Skill \"{item.Detail}\", which is not in any catalog",
            SkillAvailability.Unknown => $"Skill \"{item.Name}\" is not in any catalog",
            _ => $"{noun} \"{item.Name}\" is not ready",
        };
    }

    /// <summary>The reason a Pack in <paramref name="state"/> is not ready.</summary>
    public static string PackReason(string title, ItemState state, string firstError, string sourceReason)
    {
        return state switch
        {
            ItemState.Broken => $"Pack \"{title}\" is broken: {firstError}",
            ItemState.Withdrawn => $"Pack \"{title}\" was withdrawn by its Source: {sourceReason}",
            _ => $"Pack \"{title}\" not downloaded",
        };
    }

    /// <summary>The reason a Skill in <paramref name="state"/> is not ready.</summary>
    public static string SkillReason(string name, ItemState state, string firstError, string sourceReason)
    {
        return state switch
        {
            ItemState.Broken => $"Skill \"{name}\" is broken: {firstError}",
            ItemState.Withdrawn => $"Skill \"{name}\" was withdrawn by its Source: {sourceReason}",
            _ => $"Skill \"{name}\" not downloaded",
        };
    }
}
