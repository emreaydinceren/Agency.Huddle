using System.Globalization;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Packs.Commands;

/// <summary><c>huddle-packs validate [&lt;path&gt;]</c> (§6.4): the same checks Huddle runs, printed.</summary>
internal static class ValidateCommand
{
    /// <summary>Runs the checks and prints them; returns the exit code.</summary>
    public static int Run(ParsedArguments arguments, DataLocation location, string currentDirectory, IFileReader files, TextWriter output, TextWriter error)
    {
        CheckContext context = location.Context;
        string sources = location.SkillSourcesDir;
        Registry registry = RegistryReader.Load(files, sources, context.ComputerName);
        ResolvedRegistry resolved = RegistryResolver.Resolve(registry, files, context, new ResolveOptions(IgnoreGrace: true));
        CheckOptions options = new(Strict: arguments.Strict, ToolVersion: HelpText.Version);

        CheckResult result;
        string? virtualPath = null;
        if (arguments.Argument is { } given)
        {
            string path = Path.GetFullPath(given, currentDirectory);
            if (!files.FileExists(path) && !files.DirectoryExists(path) && !Path.IsPathRooted(given))
            {
                path = Path.GetFullPath(given, sources);
            }

            bool exists = files.FileExists(path) || files.DirectoryExists(path);
            bool all = string.Equals(path, registry.FilePath, StringComparison.OrdinalIgnoreCase) || (Checks.IsUnder(path, sources) && Checks.IsUnder(sources, path));
            bool matches = registry.AllRows.Any(row => Checks.RowReaches(resolved, row, path));
            if (all)
            {
                result = Checks.Run(resolved, context, options, files);
            }
            else if (matches)
            {
                result = Checks.Run(resolved, context, options with { OnlyPath = path }, files);
            }
            else if (!exists)
            {
                error.WriteLine($"Path not found: {path}");
                return 2;
            }
            else if (files.FileExists(path) && (path.EndsWith(PackFile.Suffix, StringComparison.OrdinalIgnoreCase) || path.EndsWith(CatalogFile.Suffix, StringComparison.OrdinalIgnoreCase)))
            {
                bool pack = path.EndsWith(PackFile.Suffix, StringComparison.OrdinalIgnoreCase);
                Registry withVirtual = pack
                    ? registry with { Packs = [.. registry.Packs, RegistryRow.Virtual(RowKind.Pack, registry.Packs.Count, path)] }
                    : registry with { Catalogs = [.. registry.Catalogs, RegistryRow.Virtual(RowKind.Catalog, registry.Catalogs.Count, path)] };
                resolved = RegistryResolver.Resolve(withVirtual, files, context, new ResolveOptions(IgnoreGrace: true));
                result = Checks.Run(resolved, context, options with { OnlyPath = path }, files);
                virtualPath = path;
            }
            else if (files.DirectoryExists(path) && files.FileExists(Path.Combine(path, "SKILL.md")))
            {
                result = Checks.RunSkillFolder(path, context, options, files);
            }
            else
            {
                error.WriteLine($"No row in SKILLS.md points at or under {path}.");
                return 2;
            }
        }
        else
        {
            result = Checks.Run(resolved, context, options, files);
        }

        Print(arguments, resolved, result, sources, virtualPath, output);
        return result.Summary.Errors > 0 ? 1 : 0;
    }

    /// <summary>Writes the lines or the JSON.</summary>
    private static void Print(ParsedArguments arguments, ResolvedRegistry resolved, CheckResult result, string sources, string? virtualPath, TextWriter output)
    {
        if (arguments.Json)
        {
            output.WriteLine(FindingWriter.Json(result, sources, arguments.Quiet));
            return;
        }

        List<OutputLine> lines = [.. result.Findings
            .Where(finding => !arguments.Quiet || finding.Severity == Severity.Error)
            .Select(finding => FindingWriter.Line(finding, sources))];
        if (!arguments.Quiet)
        {
            foreach (RowCheck row in result.Rows.Where(static row => row.Errors == 0))
            {
                RegistryRow source = row.Kind == RowKind.Pack ? resolved.Registry.Packs[row.Index] : resolved.Registry.Catalogs[row.Index];
                string message = source.IsVirtual || string.Equals(source.LocalPath.FirstText, virtualPath, StringComparison.Ordinal)
                    ? $"{FindingWriter.Display(row.LocalPath, sources)} is not in SKILLS.md yet; add a row"
                    : string.Create(CultureInfo.InvariantCulture, $"{source.TableName} row {row.Index + 1} points at {source.LocalPath.FirstText}");
                string file = source.IsVirtual ? FindingWriter.Display(row.LocalPath, sources) : RegistryReader.FileName;
                lines.Add(new OutputLine("OK", string.Empty, file, message));
            }
        }

        FindingWriter.WriteLines(output, lines);
        output.WriteLine(FindingWriter.Summary(result.Summary));
    }
}
