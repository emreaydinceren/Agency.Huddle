namespace Agency.Huddle.App.SkillSources;

/// <summary>What happens to the Agents that hold a Pack being removed (§6.12.2).</summary>
internal enum HolderChoice
{
    /// <summary>No Agent holds it.</summary>
    None,

    /// <summary>Keep their Skills: drop only the <c>_packs</c> entry.</summary>
    Keep,

    /// <summary>Remove from Agents too: take off the Skills nobody else gave them and the phrase.</summary>
    RemoveToo,
}
