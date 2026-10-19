namespace Agency.Huddle.App.SkillSources;

/// <summary>What happens to the Teammates that hold a Pack being removed (§6.12.2).</summary>
internal enum HolderChoice
{
    /// <summary>No Teammate holds it.</summary>
    None,

    /// <summary>Keep their Skills: drop only the <c>_packs</c> entry.</summary>
    Keep,

    /// <summary>Remove from Teammates too: take off the Skills nobody else gave them and the phrase.</summary>
    RemoveToo,
}
