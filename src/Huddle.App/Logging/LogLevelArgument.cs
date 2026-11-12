using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Logging;

/// <summary>
/// Turns the short <c>--log-level LEVEL</c> option into the configuration key it stands for, so a
/// Human can raise what the console shows without typing a log-category path.
/// </summary>
/// <remarks>
/// The console shows only errors by default (<c>Logging:Console:LogLevel</c> in
/// <c>appsettings.json</c>). The option sets <see cref="ConsoleKey"/>, the level of Huddle's own
/// categories on the console only: framework categories stay at the console's default, so
/// <c>--log-level Debug</c> does not bring ASP.NET Core's debug output with it, and the file log keeps
/// its own levels. The full form, <c>--Logging:Console:LogLevel:Default=Debug</c>, still reaches
/// every category. Nothing new is read anywhere: the translated argument goes to the ordinary
/// command-line configuration source.
/// </remarks>
internal static class LogLevelArgument
{
    /// <summary>The option name, as typed.</summary>
    internal const string Name = "--log-level";

    /// <summary>The configuration key the option sets.</summary>
    internal const string ConsoleKey = "Logging:Console:LogLevel:Agency.Huddle";

    /// <summary>The values the option accepts, in severity order; matched ignoring case.</summary>
    private static readonly string[] Levels = Enum.GetNames<LogLevel>();

    /// <summary>
    /// The usage line printed when the option is malformed. Spelled out rather than built from
    /// <see cref="Levels"/>, so the text a Human reads is the text the tests pin.
    /// </summary>
    internal const string Usage = "--log-level needs one of: Trace, Debug, Information, Warning, Error, Critical, None.";

    /// <summary>
    /// Replaces every <c>--log-level LEVEL</c> or <c>--log-level=LEVEL</c> in <paramref name="args"/>
    /// with <c>--Logging:Console:LogLevel:Agency.Huddle=LEVEL</c> and passes every other argument
    /// through in order. When the option appears more than once, the last one wins, as any repeated
    /// command-line key does.
    /// </summary>
    /// <param name="args">The process's command-line arguments.</param>
    /// <param name="hostArgs">The arguments to hand to the host builder, when this returns true.</param>
    /// <param name="error">Why the option was refused, when this returns false.</param>
    /// <returns>False when the option has no value or a value that is not a log level.</returns>
    internal static bool TryTranslate(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out string[]? hostArgs,
        [NotNullWhen(false)] out string? error)
    {
        List<string> translated = new(args.Count);
        int index = 0;
        while (index < args.Count)
        {
            string argument = args[index];
            index++;

            string? value;
            if (string.Equals(argument, Name, StringComparison.OrdinalIgnoreCase))
            {
                value = index < args.Count ? args[index] : null;
                index++;
            }
            else if (argument.StartsWith(Name + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = argument[(Name.Length + 1)..];
            }
            else
            {
                translated.Add(argument);
                continue;
            }

            string? level = Canonical(value);
            if (level is null)
            {
                hostArgs = null;
                error = value is null ? Usage : $"'{value}' is not a log level. {Usage}";
                return false;
            }

            translated.Add($"--{ConsoleKey}={level}");
        }

        hostArgs = [.. translated];
        error = null;
        return true;
    }

    /// <summary>
    /// The <see cref="LogLevel"/> name <paramref name="value"/> spells, ignoring case, or null. Only
    /// names count: <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> would also take
    /// <c>"3"</c> or <c>"42"</c>.
    /// </summary>
    /// <param name="value">The value typed after the option, if any.</param>
    /// <returns>The level's own spelling, or null when <paramref name="value"/> names no level.</returns>
    private static string? Canonical(string? value) =>
        Array.Find(Levels, level => string.Equals(level, value, StringComparison.OrdinalIgnoreCase));
}
