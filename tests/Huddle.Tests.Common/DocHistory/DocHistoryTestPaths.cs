using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Library paths and entries for History tests that never touch a real Library Root: the store only reads the root id and the relative path.</summary>
internal static class DocHistoryTestPaths
{
    /// <summary>A pinned root with the id <c>vault</c>.</summary>
    internal static readonly LibraryRoot Vault = new("vault", "Vault", Path.Combine(Path.GetTempPath(), "vault"), LibraryRootKind.Pinned);

    /// <summary>A document under <see cref="Vault"/>.</summary>
    /// <param name="relativePath">The path inside the root, with forward slashes.</param>
    /// <returns>The document's path.</returns>
    internal static LibraryPath Document(string relativePath) =>
        new(Vault, relativePath, Path.Combine(Vault.FullPath, relativePath), LibraryNodeRole.File);

    /// <summary>A folder under <see cref="Vault"/>.</summary>
    /// <param name="relativePath">The path inside the root, with forward slashes.</param>
    /// <returns>The folder's path.</returns>
    internal static LibraryPath Folder(string relativePath) =>
        new(Vault, relativePath, Path.Combine(Vault.FullPath, relativePath), LibraryNodeRole.Folder);

    /// <summary>An entry at a fixed UTC time on 2026-10-18.</summary>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="actor">The author.</param>
    /// <param name="kind">What the author did.</param>
    /// <param name="summary">The author's words.</param>
    /// <param name="actorKind">Agent unless stated.</param>
    /// <returns>The entry.</returns>
    internal static DocHistoryEntry At(int hour, int minute, string actor, DocChangeKind kind, string summary = "", DocActorKind actorKind = DocActorKind.Agent) =>
        new(new DateTimeOffset(2026, 10, 18, hour, minute, 0, TimeSpan.Zero), actor, actorKind, kind, summary);
}
