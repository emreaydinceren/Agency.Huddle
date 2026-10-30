namespace Agency.Huddle.Seeder.Model;

/// <summary>The gender a scenario reads from an Agent's name; its picture's <c>gender_presentation</c> must match (Seeder spec SEED-AV-2).</summary>
internal enum SeedGender
{
    /// <summary>A man's name; the library word is <c>man</c>.</summary>
    Man,

    /// <summary>A woman's name; the library word is <c>woman</c>.</summary>
    Woman,
}
