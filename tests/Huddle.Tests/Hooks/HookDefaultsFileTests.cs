namespace Agency.Huddle.Tests.Hooks;

using System.Text.Json;
using Agency.Huddle.App.Hooks;

/// <summary>
/// Anti-drift check for the shipped default-restore file, <c>src/Huddle.App/hooks.default.json</c>.
/// </summary>
/// <remarks>
/// The file is generated from <see cref="HookCatalog"/> once, by hand, when a default changes, rather
/// than being produced at build time; without this test that generation step could simply be
/// forgotten, and the file would silently drift from what the application actually does bare. That
/// would defeat its purpose: a user restoring "the known-good original" would get a stale one instead.
/// </remarks>
public sealed class HookDefaultsFileTests
{
    /// <summary>The checked-in file carries exactly the 22 keys in <see cref="HookCatalog"/>, no more and no fewer.</summary>
    [Fact]
    public void DefaultsFile_HasExactlyTheCatalogKeys()
    {
        var fromFile = ReadDefaultsFile();
        var fromCatalog = HookCatalog.All.Select(hook => hook.Key).ToList();

        Assert.Equal(fromCatalog.Count, fromFile.Count);
        foreach (var key in fromCatalog)
        {
            Assert.True(
                fromFile.ContainsKey(key),
                $"'{DefaultsFilePath()}' is missing key '{key}', which exists in HookCatalog. " +
                "Regenerate the file from HookCatalog.All (see HookCatalog's defaults) rather than hand-editing it.");
        }
    }

    /// <summary>Every value in the checked-in file matches that hook's <see cref="HookDefinition.Default"/>, byte-for-byte after line-ending normalisation.</summary>
    [Fact]
    public void DefaultsFile_ValuesMatchCatalogDefaults()
    {
        var fromFile = ReadDefaultsFile();

        foreach (var hook in HookCatalog.All)
        {
            Assert.True(
                fromFile.TryGetValue(hook.Key, out var fileValue),
                $"'{DefaultsFilePath()}' is missing key '{hook.Key}', which exists in HookCatalog. " +
                "Regenerate the file from HookCatalog.All rather than hand-editing it.");

            var normalizedFile = Normalize(fileValue);
            var normalizedCatalog = Normalize(hook.Default);

            Assert.True(
                string.Equals(normalizedCatalog, normalizedFile, StringComparison.Ordinal),
                $"'{DefaultsFilePath()}' key '{hook.Key}' no longer matches HookCatalog's Default for that hook. " +
                "This file is generated FROM HookCatalog, not maintained independently: when a Default changes in " +
                "HookCatalog.cs, regenerate hooks.default.json by serializing HookCatalog.All (key -> Default) with " +
                "the same indented JsonSerializerOptions HookStore uses (ProtocolJson.Options with WriteIndented = " +
                "true), and commit the result. Do not hand-edit this file to make the test pass.");
        }
    }

    /// <summary>
    /// Every value in the checked-in file matches that hook's <see cref="HookDefinition.Default"/>
    /// byte-for-byte, with NO line-ending normalisation on either side. This is the check
    /// <see cref="DefaultsFile_ValuesMatchCatalogDefaults"/> cannot do — its <see cref="Normalize"/>
    /// step keeps that test stable, but it also erases any drift between the file's own line endings
    /// and the catalog's, which is exactly the drift that once let <c>hooks.default.json</c> ship
    /// LF-only against a <see cref="HookDefinition.Default"/> that, before it normalised itself on
    /// construction, could compile as CRLF depending on how <c>HookCatalog.cs</c> was checked out.
    /// <see cref="HookDefinition.Default"/> now normalises to <c>\n</c> unconditionally, so this test
    /// no longer depends on checkout platform either — it exists to guard that normalisation itself:
    /// if someone changes <see cref="HookDefinition"/> to stop normalising, or hand-edits the file
    /// with different line endings than <see cref="HookCatalog"/> produces, this is what fails.
    /// </summary>
    [Fact]
    public void DefaultsFile_ValuesMatchCatalogDefaults_WithLineEndingsPreserved()
    {
        var fromFile = ReadDefaultsFile();

        foreach (var hook in HookCatalog.All)
        {
            Assert.True(
                fromFile.TryGetValue(hook.Key, out var fileValue),
                $"'{DefaultsFilePath()}' is missing key '{hook.Key}', which exists in HookCatalog.");

            Assert.True(
                string.Equals(hook.Default, fileValue, StringComparison.Ordinal),
                $"'{DefaultsFilePath()}' key '{hook.Key}' does not match HookCatalog's Default byte-for-byte. " +
                "HookDefinition normalises every Default to '\\n' line endings on construction, precisely so " +
                "model-facing text does not depend on which platform checked HookCatalog.cs out; this file is " +
                "expected to carry that same normalised '\\n' form. Regenerate hooks.default.json from " +
                "HookCatalog.All (key -> Default) rather than hand-editing it.");
        }
    }

    /// <summary>Normalises line endings so the comparison is stable between a CRLF working-tree checkout and the <c>\n</c> raw string literals in <see cref="HookCatalog"/>.</summary>
    /// <param name="text">The text to normalise.</param>
    /// <returns><paramref name="text"/> with every <c>\r\n</c> replaced by <c>\n</c>.</returns>
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Deserialises the checked-in default-restore file into a plain key/value map.</summary>
    /// <returns>Every key/value pair the file currently holds.</returns>
    private static Dictionary<string, string> ReadDefaultsFile()
    {
        var path = DefaultsFilePath();
        var json = File.ReadAllText(path);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        Assert.NotNull(values);
        return values;
    }

    /// <summary>
    /// Locates <c>src/Huddle.App/hooks.default.json</c> by walking up from the test binary's own
    /// directory until <c>Huddle.slnx</c> is found, the same approach
    /// <c>TeamWebApplicationFactory.FindAppContentRoot</c> and <c>PromptGoldenTests.GoldenFilePath</c> use.
    /// </summary>
    /// <returns>The default-restore file's full path.</returns>
    private static string DefaultsFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return Path.Combine(directory.FullName, "src", "Huddle.App", "hooks.default.json");
    }
}
