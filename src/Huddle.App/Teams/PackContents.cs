using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Teams;

/// <summary>What a Pack puts into a Teammate file: its <c>_packs</c> entry, its Skills and its <c>consult_when</c> clause.</summary>
/// <param name="Id">The Pack id.</param>
/// <param name="Version">The Pack file's version, written to <c>_packs</c>.</param>
/// <param name="ConsultWhenAdds">The clause added to <c>consult_when</c>, or null.</param>
/// <param name="SkillNames">The Pack's Skills.</param>
internal sealed record PackContents(string Id, string Version, string? ConsultWhenAdds, IReadOnlyList<string> SkillNames)
{
    /// <summary>The contents of a Pack from the registry.</summary>
    public static PackContents From(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new PackContents(pack.Id, pack.Version, pack.ConsultWhenAdds, pack.SkillNames);
    }
}
