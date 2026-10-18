using System.Text;
using System.Text.Json;
using Agency.Huddle.Packs.Commands;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.Packs;

/// <summary>The <c>huddle-packs</c> entry point (§6.4). It writes no file and opens no network connection.</summary>
internal static class Program
{
    /// <summary>Runs the command with the process's console, environment and folders.</summary>
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return Run(args, Console.Out, Console.Error, Environment.GetEnvironmentVariable, Environment.CurrentDirectory, AppContext.BaseDirectory);
    }

    /// <summary>Runs one command; everything it prints goes to the two writers, so a test runs it in-process.</summary>
    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<string, string?> getEnvironmentVariable,
        string currentDirectory,
        string executableDirectory,
        IFileReader? files = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        IFileReader reader = files ?? PhysicalFileReader.Instance;
        ParsedArguments? arguments = ArgumentParser.Parse(args, error);
        if (arguments is null)
        {
            return 2;
        }

        try
        {
            return Dispatch(arguments, output, error, getEnvironmentVariable, currentDirectory, executableDirectory, reader);
        }
        catch (IOException exception)
        {
            error.WriteLine($"Cannot read: {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            error.WriteLine($"Cannot read: {exception.Message}");
            return 2;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"Cannot read: {exception.Message}");
            return 2;
        }
    }

    /// <summary>Runs the parsed command.</summary>
    private static int Dispatch(ParsedArguments arguments, TextWriter output, TextWriter error, Func<string, string?> environment, string currentDirectory, string executableDirectory, IFileReader files)
    {
        switch (arguments.Command)
        {
            case "version":
                output.WriteLine($"huddle-packs {HelpText.Version} (schemaVersion {PackFile.KnownSchemaVersion})");
                return 0;
            case "schema":
                return SchemaCommand.Run(arguments.Argument ?? string.Empty, output, error);
        }

        string dataDir = DataLocator.DataDir(arguments.DataDir, environment, currentDirectory, executableDirectory, files);
        if (arguments.Command == "help")
        {
            output.Write(HelpText.Usage);
            if (DataLocator.Locate(dataDir, files).Location is not null)
            {
                output.WriteLine($"Install file: {Path.Combine(dataDir, "InstallSkillPack.md")}");
            }

            return 0;
        }

        LocateResult located = DataLocator.Locate(dataDir, files);
        if (located.Location is not { } location)
        {
            error.WriteLine(located.Error);
            return 2;
        }

        return arguments.Command switch
        {
            "validate" => ValidateCommand.Run(arguments, location, currentDirectory, files, output, error),
            "roles" => RolesCommand.Run(arguments, location.Context, output),
            "installed" => InstalledCommand.Run(arguments, location.Context, output),
            _ => SkillsCommand.Run(arguments, location, files, output),
        };
    }
}
