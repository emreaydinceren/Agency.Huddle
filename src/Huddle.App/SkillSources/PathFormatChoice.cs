namespace Agency.Huddle.App.SkillSources;

/// <summary>The Human's last choice of <see cref="PathFormat"/> for this session (not saved), so the prompt dialog, the
/// Sources tab and Copy fix prompt agree. Registered as a scoped service.</summary>
internal sealed class PathFormatChoice
{
    /// <summary>The chosen format; Windows until the Human picks another.</summary>
    public PathFormat Format { get; set; } = PathFormat.Windows;
}
