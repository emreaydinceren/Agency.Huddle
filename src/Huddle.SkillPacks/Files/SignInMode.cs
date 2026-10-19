namespace Agency.Huddle.SkillPacks.Files;

/// <summary>Whether reaching the Source needs a sign-in. Never a credential.</summary>
public enum SignInMode
{
    /// <summary>Public.</summary>
    None,

    /// <summary>The AI tool needs the Human's sign-in.</summary>
    Required,
}
