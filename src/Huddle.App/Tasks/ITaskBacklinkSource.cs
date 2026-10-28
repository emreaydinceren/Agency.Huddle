using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks;

/// <summary>The open Tasks that link to a Library document, for the Backlinks panel.</summary>
internal interface ITaskBacklinkSource
{
    /// <summary>Raised, possibly off the renderer thread, when the Task list changed and a result from <see cref="For"/> may be stale.</summary>
    event Action? Changed;

    /// <summary>The open Tasks whose description links to <paramref name="document"/>, in the Task store's order.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>One row per linking Task; empty when none links.</returns>
    IReadOnlyList<TaskBacklink> For(LibraryPath document);
}
