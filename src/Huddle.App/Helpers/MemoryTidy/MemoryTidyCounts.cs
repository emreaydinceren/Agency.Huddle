using System.Globalization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// What one pass changed, counted from its journal: the numbers of the list's summary line and of the Feed item (Memory Tidy spec §6.13).
/// </summary>
/// <param name="Tidied">Topic files merged into, rewritten or re-described.</param>
/// <param name="Dropped">Topic files dropped, or mostly emptied by a rewrite. Files folded in by a merge are not counted.</param>
/// <param name="Folders">Memory folders where a step changed something.</param>
internal sealed record MemoryTidyCounts(int Tidied, int Dropped, int Folders)
{
    /// <summary>Whether any topic file changed. A pass that changed none posts no Feed item.</summary>
    public bool ChangedAnyFile => this.Tidied + this.Dropped > 0;

    /// <summary>
    /// What the pass did, in words, starting lower case so the Feed line can follow the Helper's name and the list can capitalise it:
    /// <c>tidied 4 topic files and dropped 2 in 3 Memory folders</c>. A clause with no files is left out, and one file or one folder is singular.
    /// </summary>
    /// <returns>The sentence without its full stop.</returns>
    public string Sentence()
    {
        string clause = (this.Tidied, this.Dropped) switch
        {
            (> 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"tidied {this.Tidied} {Files(this.Tidied)} and dropped {this.Dropped}"),
            (_, > 0) => string.Create(CultureInfo.InvariantCulture, $"dropped {this.Dropped} {Files(this.Dropped)}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"tidied {this.Tidied} {Files(this.Tidied)}"),
        };

        return string.Create(CultureInfo.InvariantCulture, $"{clause} in {this.Folders} {(this.Folders == 1 ? "Memory folder" : "Memory folders")}");
    }

    private static string Files(int count) => count == 1 ? "topic file" : "topic files";
}
