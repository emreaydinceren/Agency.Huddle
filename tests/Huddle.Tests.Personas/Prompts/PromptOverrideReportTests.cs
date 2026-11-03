using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>
/// Pins SB-R17's "report, never refuse" (Spec 7.5, ADR-0007): an override that breaks a prompt's contract is
/// still used as written and reported by <see cref="PromptValidator"/>, and a file that names no prompt is
/// listed by <see cref="PromptStore.UnknownOverrides"/> and never used.
/// </summary>
public sealed class PromptOverrideReportTests
{
    // systemPrompt.orientation (PromptCatalog.cs:105) requires "{{helpTool}}" (PromptCatalog.cs:121).
    private const string Key = "systemPrompt.orientation";

    /// <summary>An override missing a required placeholder is returned as written, and the validator reports an Error for that key.</summary>
    [Fact]
    public void Raw_OverrideMissingRequiredPlaceholder_IsUsedAndReportedAsError()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key + ".md", "No tool name in here.");

        using PromptStore store = NewStore(dataDir);

        Assert.Equal("No tool name in here.", store.Raw(Key));
        IReadOnlyList<PromptIssue> issues = PromptValidator.Validate(PromptCatalog.Get(Key), store.Raw(Key));
        Assert.Contains(issues, issue => issue.Severity == PromptIssueSeverity.Error && string.Equals(issue.Key, Key, StringComparison.Ordinal));
    }

    /// <summary>A file <c>notAKey.md</c> is listed exactly once as unknown and does not change any prompt.</summary>
    [Fact]
    public void UnknownOverrides_FileNamingNoPrompt_IsListedAndNotUsed()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, "notAKey.md", "Stray text.");

        using PromptStore store = NewStore(dataDir);

        Assert.Equal(["notAKey"], store.UnknownOverrides);
        Assert.Equal(PromptCatalog.Get(Key).Default, store.Raw(Key));
    }

    /// <summary>A file named after a known key is not listed, and the list is sorted ordinally without the extension.</summary>
    [Fact]
    public void UnknownOverrides_KnownKeyFile_IsNotListed()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key + ".md", "Override {{helpTool}}.");
        WriteFile(dataDir.Path, "zeta.md", "z");
        WriteFile(dataDir.Path, "alpha.md", "a");

        using PromptStore store = NewStore(dataDir);

        Assert.Equal(["alpha", "zeta"], store.UnknownOverrides); // ordinal sort of the two stray stems
    }

    /// <summary>Builds a store over <paramref name="dataDir"/> with the default Prompts folder.</summary>
    /// <param name="dataDir">The temp data directory.</param>
    private static PromptStore NewStore(TempDataDir dataDir) => new(dataDir.Options(), NullLogger<PromptStore>.Instance);

    /// <summary>Writes one file into the Prompts folder.</summary>
    /// <param name="dataDir">The data directory root.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="text">The file text.</param>
    private static void WriteFile(string dataDir, string fileName, string text)
    {
        string dir = Path.Combine(dataDir, "Prompts");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), text);
    }
}
