namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Where a Skills tab row comes from, for its Source column.</summary>
internal enum SkillRowOrigin
{
    /// <summary>Shipped with Huddle (a default, or a default overridden on disk).</summary>
    BuiltIn,

    /// <summary>A folder put straight into <c>Skills/</c> with no <c>.source.json</c>.</summary>
    Yours,

    /// <summary>Known to the registry: fetched by the AI tool, copied, or listed but not on this computer yet.</summary>
    Registry,
}
