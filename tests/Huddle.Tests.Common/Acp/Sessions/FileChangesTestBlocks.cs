using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Builds the <c>HookBlocks</c> a File Changes report becomes, for a test that hands a Turn its report directly.</summary>
public static class FileChangesTestBlocks
{
    /// <summary>Renders <paramref name="report"/> the way the File Changes hook does and wraps the block as a Turn's hook blocks.</summary>
    /// <param name="report">The report, or <see langword="null"/> for none.</param>
    /// <param name="prompts">Supplies the block's wording.</param>
    /// <returns><see langword="null"/> when nothing renders (no report, or an empty one); otherwise a list of the one block.</returns>
    internal static IReadOnlyList<string>? Of(FileChangesReport? report, IPromptSource prompts)
    {
        string? block = FileChangesTurnHook.Render(report, prompts);
        return block is null ? null : [block];
    }
}
