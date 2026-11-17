using System.Globalization;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Fills the <c>helper.memoryTidy.folder</c> Prompt from a <see cref="MemoryTidyInput"/> (Memory Tidy spec §6.9). A file's version is never shown.</summary>
internal static class MemoryTidyPrompt
{
    /// <summary>The sentence that tells the model descriptions do not change in a folder.</summary>
    internal const string LockedDescriptions = "In this folder descriptions do not change: give no \"describe\" action and no \"description\" at all.";

    /// <summary>The values of the folder Prompt's placeholders; each key is the whole token, braces included.</summary>
    /// <param name="input">The input.</param>
    internal static Dictionary<string, string> Values(MemoryTidyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string files = input.Files.Count == 0
            ? "none"
            : string.Join("\n\n", input.Files.Select(static file => $"<file name=\"{file.File}\">\n{file.Text.TrimEnd()}\n</file>"));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{owner}}"] = input.Owner,
            ["{{today}}"] = input.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{{maxActions}}"] = input.Limits.MaxActions.ToString(CultureInfo.InvariantCulture),
            ["{{maxDrops}}"] = input.Limits.MaxDrops.ToString(CultureInfo.InvariantCulture),
            ["{{maxAnswerChars}}"] = input.Limits.MaxAnswerChars.ToString(CultureInfo.InvariantCulture),
            ["{{descriptions}}"] = input.Limits.DescriptionsLocked ? LockedDescriptions : string.Empty,
            ["{{index}}"] = input.Index.Count == 0 ? "(empty)" : string.Join('\n', input.Index),
            ["{{files}}"] = files,
        };
    }

    /// <summary>The folder Prompt's default text with <paramref name="input"/>'s values in place.</summary>
    /// <param name="input">The input.</param>
    internal static string Render(MemoryTidyInput input)
    {
        string text = PromptCatalog.Get("helper.memoryTidy.folder").Default;
        foreach ((string token, string value) in Values(input))
        {
            text = text.Replace(token, value, StringComparison.Ordinal);
        }

        return text;
    }
}
