using Agency.Huddle.App.Library;
using Agency.Huddle.SkillPacks.Reading;
using System.Text;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Deletes a row's lines from SKILLS.md (§8.8): every other byte is written back as it was, through a temporary
/// file and a move. If the file changed between the read and the write (by length or write time), it starts again, at
/// most three times. Huddle never reformats SKILLS.md.</summary>
internal sealed class SkillsMdWriter(SkillSourcesSettings settings)
{
    private const int Attempts = 3;

    /// <summary>The full path of SKILLS.md.</summary>
    public string FilePath => Path.Combine(settings.Dir, RegistryReader.FileName);

    /// <summary>Removes the first row of <paramref name="kind"/> whose Local path resolves, for this computer, to
    /// <paramref name="resolvedFile"/>.</summary>
    public RemoveRowResult RemoveRow(RowKind kind, string resolvedFile)
    {
        ArgumentNullException.ThrowIfNull(resolvedFile);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            if (!File.Exists(this.FilePath))
            {
                return RemoveRowResult.NotFound;
            }

            FileInfo before = new(this.FilePath);
            long length = before.Length;
            DateTime written = before.LastWriteTimeUtc;
            byte[] bytes = File.ReadAllBytes(this.FilePath);
            byte[]? edited = Remove(bytes, kind, resolvedFile, settings.Dir, settings.ComputerName);
            if (edited is null)
            {
                return RemoveRowResult.NotFound;
            }

            FileInfo now = new(this.FilePath);
            if (now.Length != length || now.LastWriteTimeUtc != written)
            {
                continue;
            }

            AtomicFile.WriteBytes(this.FilePath, edited);
            return RemoveRowResult.Removed;
        }

        return RemoveRowResult.KeptChanging;
    }

    /// <summary>The file's bytes without the matching row's lines, or null when no row matches. Pure, for tests.</summary>
    public static byte[]? Remove(byte[] bytes, RowKind kind, string resolvedFile, string registryDir, string computer)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        string text = Encoding.UTF8.GetString(bytes);
        Registry registry = RegistryReader.Read(text, registryDir, computer);
        IReadOnlyList<RegistryRow> rows = kind == RowKind.Pack ? registry.Packs : registry.Catalogs;
        RegistryRow? row = rows.FirstOrDefault(row => row.LocalPath.Entries.Any(entry =>
            string.Equals(PathEntries.MakeFull(entry.Path, registryDir), Path.GetFullPath(resolvedFile), StringComparison.OrdinalIgnoreCase)));
        if (row is null)
        {
            return null;
        }

        List<(int Start, int Length)> lines = SplitLines(bytes);
        int first = row.Span.FirstLine - 1;
        int last = row.Span.LastLine - 1;
        using MemoryStream output = new(bytes.Length);
        for (int i = 0; i < lines.Count; i++)
        {
            if (i < first || i > last)
            {
                output.Write(bytes, lines[i].Start, lines[i].Length);
            }
        }

        return output.ToArray();
    }

    /// <summary>The byte ranges of each line, each including its line ending (LF, CRLF or CR).</summary>
    private static List<(int Start, int Length)> SplitLines(byte[] bytes)
    {
        List<(int, int)> lines = [];
        int start = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n' || (bytes[i] == (byte)'\r' && (i + 1 >= bytes.Length || bytes[i + 1] != (byte)'\n')))
            {
                lines.Add((start, i - start + 1));
                start = i + 1;
            }
        }

        if (start < bytes.Length)
        {
            lines.Add((start, bytes.Length - start));
        }

        return lines;
    }
}
