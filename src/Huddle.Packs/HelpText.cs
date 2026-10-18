using System.Reflection;

namespace Agency.Huddle.Packs;

/// <summary>The usage text and the version line (§6.4).</summary>
internal static class HelpText
{
    /// <summary>The usage block, with a trailing newline.</summary>
    public const string Usage = """
        huddle-packs <command> [options]

          validate [<path>]        Check SKILLS.md and what it points at, or only the rows at <path>
          roles                    List the role ids a Pack may name
          installed                List the Skills installed in Huddle
          skills [<text>]          List the Skills in every catalog and Pack
          schema pack|catalog|source
                                   Print a JSON Schema
          --version, --help

          --data-dir <path>        Huddle's data folder (default: HUDDLE_DATA_DIR, else App_Data)
          --json                   Print JSON
          --quiet                  validate: errors and the summary only
          --strict                 validate: a Skill or file that is not downloaded is an error

        Exit codes: 0 no errors, 1 errors found, 2 could not run.

        """;

    /// <summary>This program's version: the informational version with any <c>+build</c> suffix cut.</summary>
    public static string Version
    {
        get
        {
            string? informational = typeof(HelpText).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string version = informational ?? typeof(HelpText).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            int plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? version : version[..plus];
        }
    }
}
