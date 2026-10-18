namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>How serious a finding is. An error makes its item Broken; a warning is shown on the details card.</summary>
public enum Severity
{
    /// <summary>Shown, but the item stays usable.</summary>
    Warning = 1,

    /// <summary>The item the finding belongs to takes the state of its check.</summary>
    Error = 2,
}

/// <summary>One thing a check found. The four fields are the whole public contract.</summary>
/// <param name="Severity">Warning or error, after <see cref="CheckOptions.Strict"/> has been applied.</param>
/// <param name="Code">A code from <see cref="CheckCatalog"/>, such as <c>S004</c>.</param>
/// <param name="File">The absolute path of the file or folder the finding is about; SKILLS.md for a finding about the registry.</param>
/// <param name="Message">Specific to this finding: names the Skill, role, column or limit. One line.</param>
public sealed record Finding(Severity Severity, string Code, string File, string Message)
{
    /// <summary>An error finding with the catalog code <paramref name="code"/>.</summary>
    public static Finding Error(string code, string file, string message)
    {
        return new Finding(Severity.Error, code, file, message);
    }

    /// <summary>A warning finding with the catalog code <paramref name="code"/>.</summary>
    public static Finding Warning(string code, string file, string message)
    {
        return new Finding(Severity.Warning, code, file, message);
    }
}
