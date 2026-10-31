namespace Agency.Huddle.Packs;

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
                    string? problem = PlacePositional(token, ref command, ref argument);
                    if (problem is not null)
                    {
                        return Fail(error, problem);
                    }

                    break;
            }
        }

        string? finalProblem = FinalProblem(command, argument);
        if (finalProblem is not null || command is null)
        {
            return Fail(error, finalProblem ?? "No command given.");
        }

        return new ParsedArguments(command, argument, dataDir, json, quiet, strict);
    }

    /// <summary>Takes a bare word as the command or its argument; returns the problem, or null when it fits.</summary>
    private static string? PlacePositional(string token, ref string? command, ref string? argument)
    {
        if (token.StartsWith('-'))
        {
            return $"Unknown option {token}.";
        }

        if (command is null)
        {
            if (!Commands.Contains(token, StringComparer.Ordinal))
            {
                return $"Unknown command {token}.";
            }

            command = token;
            return null;
        }

        if (argument is null && command is "validate" or "skills" or "schema")
        {
            argument = token;
            return null;
        }

        return $"Unexpected argument {token}.";
    }

    /// <summary>The problem with the finished command line, or null when there is none.</summary>
    private static string? FinalProblem(string? command, string? argument)
    {
        if (command is null)
        {
            return "No command given.";
        }

        if (command == "schema" && argument is not ("pack" or "catalog" or "source"))
        {
            return argument is null ? "schema needs pack, catalog or source." : $"Unknown schema {argument}.";
        }

        return null;
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
