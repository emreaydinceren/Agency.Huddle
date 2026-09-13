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
