using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Writes the History of Library Markdown documents the way the app's <c>DocHistoryStore</c> does: one JSON Lines file per
/// document at <c>doc-history/{rootId}/{relativePath}.jsonl</c>, oldest entry first. The seeder cannot see the store's
/// internals, so the path and the line format are mirrored here and pinned by a test that compares them with the store.
/// </summary>
internal static class DocHistoryWriter
{
    /// <summary>The <c>v</c> every line carries, mirroring <c>DocHistoryStore.FormatVersion</c>.</summary>
    private const int FormatVersion = 1;

    /// <summary>The same options as the store's: one unindented line, camelCase names and enum values, relaxed escaping.</summary>
    private static readonly JsonSerializerOptions LineOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>The data folder's first segment and the built-in Library root it belongs to.</summary>
    private static readonly Dictionary<string, string> RootIdByFolder = new(StringComparer.Ordinal)
    {
        ["Teams"] = "teams",
        ["Teammates"] = "teammates",
        ["Uploads"] = "uploads",
    };

    /// <summary>The history file of a document, relative to the data folder.</summary>
    /// <param name="relativePath">The document's path below the data folder, using <c>/</c>.</param>
    /// <returns>A path such as <c>doc-history/teams/Platform/Architecture.md.jsonl</c>.</returns>
    /// <exception cref="InvalidOperationException">The path is not under a built-in Library root.</exception>
    internal static string HistoryPath(string relativePath)
    {
        int slash = relativePath.IndexOf('/', StringComparison.Ordinal);
        string folder = slash < 0 ? relativePath : relativePath[..slash];
        if (slash < 0 || !RootIdByFolder.TryGetValue(folder, out string? rootId))
        {
            throw new InvalidOperationException($"The document {relativePath} is not under Teams/, Teammates/ or Uploads/.");
        }

        return $"doc-history/{rootId}/{relativePath[(slash + 1)..]}.jsonl";
    }

    /// <summary>The entry a seeded edit stands for.</summary>
    /// <param name="edit">The seeded edit.</param>
    /// <param name="today">The run date its offset is measured from.</param>
    /// <returns>The entry.</returns>
    internal static DocHistoryEntry Entry(SeedDocEdit edit, DateOnly today) =>
        new(TaskFileWriter.At(today, -edit.DaysAgo, edit.Time), edit.Actor, edit.ActorKind, edit.Kind, edit.Summary);

    /// <summary>One entry as one line, without a line break.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The JSON line.</returns>
    internal static string Serialize(DocHistoryEntry entry) =>
        JsonSerializer.Serialize(new HistoryLine(FormatVersion, entry.At, entry.Actor, entry.ActorKind, entry.Kind, entry.Summary), LineOptions);

    /// <summary>Writes every document's history file, its entries oldest first.</summary>
    /// <param name="plan">The plan holding <see cref="SeedPlan.DocHistory"/>.</param>
    /// <param name="dataDir">The data folder.</param>
    internal static void Write(SeedPlan plan, string dataDir)
    {
        foreach (IGrouping<string, SeedDocEdit> document in plan.DocHistory.GroupBy(static edit => edit.RelativePath, StringComparer.Ordinal))
        {
            StringBuilder text = new();
            foreach (DocHistoryEntry entry in document.Select(edit => Entry(edit, plan.Today)).OrderBy(static entry => entry.At))
            {
                text.Append(Serialize(entry)).Append('\n');
            }

            SeedIo.WriteText(SeedIo.Resolve(dataDir, HistoryPath(document.Key)), text.ToString());
        }
    }

    /// <summary>The on-disk shape of one line, in the store's member order.</summary>
    /// <param name="V">The format version.</param>
    /// <param name="At">When the entry was written.</param>
    /// <param name="Actor">The author's Name.</param>
    /// <param name="ActorKind">Agent or Human.</param>
    /// <param name="Kind">What the author did.</param>
    /// <param name="Summary">The author's words.</param>
    private sealed record HistoryLine(int V, DateTimeOffset At, string Actor, DocActorKind ActorKind, DocChangeKind Kind, string Summary);
}
