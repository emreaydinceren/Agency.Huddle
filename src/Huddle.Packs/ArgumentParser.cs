namespace Agency.Huddle.Packs;

/// <summary>What the command line asked for.</summary>
/// <param name="Command"><c>validate</c>, <c>roles</c>, <c>installed</c>, <c>skills</c>, <c>schema</c>, <c>version</c> or <c>help</c>.</param>
/// <param name="Argument">The path, the search text or the schema kind.</param>
/// <param name="DataDir">The <c>--data-dir</c> value.</param>
/// <param name="Json"><c>--json</c>.</param>
/// <param name="Quiet"><c>--quiet</c>.</param>
/// <param name="Strict"><c>--strict</c>.</param>
internal sealed record ParsedArguments(string Command, string? Argument, string? DataDir, bool Json, bool Quiet, bool Strict);

/// <summary>Parses the command line by hand, in the style of <c>Huddle.Console</c> (§6.4).</summary>
internal static class ArgumentParser
{
    private static readonly string[] Commands = ["validate", "roles", "installed", "skills", "schema"];

    /// <summary>The parsed arguments, or null after writing the usage to <paramref name="error"/>.</summary>
    public static ParsedArguments? Parse(IReadOnlyList<string> args, TextWriter error)
    {
        string? command = null;
        string? argument = null;
        string? dataDir = null;
        bool json = false;
        bool quiet = false;
        bool strict = false;
        for (int i = 0; i < args.Count; i++)
        {
            string token = args[i];
            switch (token)
            {
                case "--data-dir":
                    if (i + 1 >= args.Count)
                    {
                        return Fail(error, "--data-dir needs a value.");
                    }

#pragma warning disable S127 // Deliberate "consume the flag's value" advance in an argv parser, bounds-checked on the line above.
                    dataDir = args[++i];
#pragma warning restore S127
                    break;
                case "--json":
                    json = true;
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--version":
                    command ??= "version";
                    break;
                case "--help" or "-h" or "help":
                    command ??= "help";
                    break;
                default:
                    if (token.StartsWith('-'))
                    {
                        return Fail(error, $"Unknown option {token}.");
                    }

                    if (command is null)
                    {
                        if (!Commands.Contains(token, StringComparer.Ordinal))
                        {
                            return Fail(error, $"Unknown command {token}.");
                        }

                        command = token;
                    }
                    else if (argument is null && command is "validate" or "skills" or "schema")
                    {
                        argument = token;
                    }
                    else
                    {
                        return Fail(error, $"Unexpected argument {token}.");
                    }

                    break;
            }
        }

        if (command is null)
        {
            return Fail(error, "No command given.");
        }

        if (command == "schema" && argument is not ("pack" or "catalog" or "source"))
        {
            return Fail(error, argument is null ? "schema needs pack, catalog or source." : $"Unknown schema {argument}.");
        }

        return new ParsedArguments(command, argument, dataDir, json, quiet, strict);
    }

    /// <summary>Writes the problem and the usage; returns null.</summary>
    private static ParsedArguments? Fail(TextWriter error, string problem)
    {
        error.WriteLine(problem);
        error.WriteLine();
        error.Write(HelpText.Usage);
        return null;
    }
}
