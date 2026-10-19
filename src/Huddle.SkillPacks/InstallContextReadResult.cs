namespace Agency.Huddle.SkillPacks;

/// <summary>What <see cref="InstallContext.Parse"/> found.</summary>
/// <param name="Value">The context, or null.</param>
/// <param name="Reason">Why it cannot be used, for the exit-2 message; null when read.</param>
/// <param name="NewerSchemaVersion">The schema version when the file is newer than this program; otherwise null.</param>
public sealed record InstallContextReadResult(InstallContext? Value, string? Reason, int? NewerSchemaVersion)
{
    /// <summary>A file that was read.</summary>
    public static InstallContextReadResult Read(InstallContext value)
    {
        return new InstallContextReadResult(value, null, null);
    }

    /// <summary>A file that cannot be read, with the reason.</summary>
    public static InstallContextReadResult Invalid(string reason)
    {
        return new InstallContextReadResult(null, reason, null);
    }

    /// <summary>A file written for a newer schema.</summary>
    public static InstallContextReadResult Newer(int schemaVersion)
    {
        return new InstallContextReadResult(null, $"schemaVersion {schemaVersion}", schemaVersion);
    }
}
