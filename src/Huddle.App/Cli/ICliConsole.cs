namespace Agency.Huddle.App.Cli;

/// <summary>The console a CLI verb writes to; a seam so tests spawn no process.</summary>
internal interface ICliConsole
{
    /// <summary>Gets standard output.</summary>
    TextWriter Out { get; }

    /// <summary>Gets standard error.</summary>
    TextWriter Error { get; }

    /// <summary>Gets a value indicating whether standard output is redirected (also true under mintty / Git Bash).</summary>
    bool IsOutputRedirected { get; }
}
