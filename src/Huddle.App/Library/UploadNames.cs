using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Turns a browser's file name into a name an upload can be saved under (spec 6.3 Names), and numbers a
/// name that is already taken. The result always passes <see cref="LibraryNames.Validate"/>.
/// </summary>
internal static class UploadNames
{
    /// <summary>The longest stem kept, before the extension.</summary>
    private const int MaxStemLength = 100;

    /// <summary>The stem given to a name that would otherwise be empty or a Windows device name.</summary>
    private const string FallbackStem = "file";

    /// <summary>The generic name browsers give a pasted image, which is replaced by a timestamped name.</summary>
    private const string GenericPasteName = "image.png";

    /// <summary>
    /// Cleans a browser's file name for an upload. A missing name, or the generic paste name, becomes
    /// <c>pasted-image-yyyyMMdd-HHmmss.png</c> in the Human's local time.
    /// </summary>
    /// <param name="browserName">The name the browser gave the file; may include a directory part.</param>
    /// <param name="clock">The clock the pasted-image timestamp is read from.</param>
    /// <returns>A name with no directory part, no refused character and a stem of at most 100 characters.</returns>
    internal static string Clean(string? browserName, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrEmpty(browserName))
        {
            return PastedImageName(clock);
        }

        string leaf = LeafName(browserName);
        if (string.Equals(leaf, GenericPasteName, StringComparison.OrdinalIgnoreCase))
        {
            return PastedImageName(clock);
        }

        string cleaned = ReplaceRefusedCharacters(leaf).TrimEnd(' ', '.');
        cleaned = CollapseDashes(cleaned);

        string extension = Path.GetExtension(cleaned);
        string stem = cleaned[..(cleaned.Length - extension.Length)];
        if (stem.Length > MaxStemLength)
        {
            stem = stem[..MaxStemLength].TrimEnd(' ', '.');
        }

        if (stem.Length == 0)
        {
            stem = FallbackStem;
        }

        return ReplaceDeviceStem(stem) + extension;
    }

    /// <summary>
    /// Numbers a name that is already taken: <c>name (n).ext</c>, with the number before the extension so the
    /// extension still names the kind of file.
    /// </summary>
    /// <param name="name">The cleaned name that is taken.</param>
    /// <param name="n">The number to insert; the first copy keeps the plain name, so this is 2 or more.</param>
    /// <returns>The name with <c>" (n)"</c> before its extension.</returns>
    internal static string FreeName(string name, int n)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 2);

        string extension = Path.GetExtension(name);
        string stem = name[..(name.Length - extension.Length)];
        return string.Create(CultureInfo.InvariantCulture, $"{stem} ({n}){extension}");
    }

    /// <summary>The timestamped name a pasted image gets, in the Human's local time.</summary>
    /// <param name="clock">The clock to read the local time from.</param>
    /// <returns><c>pasted-image-yyyyMMdd-HHmmss.png</c>.</returns>
    private static string PastedImageName(TimeProvider clock)
    {
        string stamp = clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"pasted-image-{stamp}.png";
    }

    /// <summary>The last part of a path, whichever separator the browser's path used.</summary>
    /// <param name="browserName">The path or bare name.</param>
    /// <returns>The text after the last <c>/</c> or <c>\</c>.</returns>
    private static string LeafName(string browserName)
    {
        int separator = browserName.LastIndexOfAny(['/', '\\']);
        return separator < 0 ? browserName : browserName[(separator + 1)..];
    }

    /// <summary>
    /// Replaces each character <see cref="LibraryNames.Validate"/> refuses with a dash: the characters
    /// <c>: * ? " &lt; &gt; |</c> and control characters (the set <see cref="LibrarySegments"/> refuses), and the
    /// backtick, which would end the code span the path is sent in.
    /// </summary>
    /// <param name="name">The name to clean.</param>
    /// <returns>The name with every refused character replaced.</returns>
    private static string ReplaceRefusedCharacters(string name)
    {
        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            bool refused = c is ':' or '<' or '>' or '"' or '|' or '?' or '*' or '`' || char.IsControl(c);
            _ = builder.Append(refused ? '-' : c);
        }

        return builder.ToString();
    }

    /// <summary>Collapses each run of dashes to one dash.</summary>
    /// <param name="name">The name to collapse.</param>
    /// <returns>The name with no two dashes in a row.</returns>
    private static string CollapseDashes(string name)
    {
        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            if (c == '-' && builder.Length > 0 && builder[^1] == '-')
            {
                continue;
            }

            _ = builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Replaces a stem whose first dot-separated part is a Windows device name (<c>CON</c>, <c>LPT9</c>...)
    /// with <c>file</c>, keeping the rest of the stem. The device rule is the one <see cref="LibrarySegments"/> applies.
    /// </summary>
    /// <param name="stem">The stem, without its extension.</param>
    /// <returns>The stem, with a device-name first part replaced.</returns>
    private static string ReplaceDeviceStem(string stem)
    {
        int firstDot = stem.IndexOf('.', StringComparison.Ordinal);
        string firstPart = firstDot < 0 ? stem : stem[..firstDot];
        if (LibrarySegments.Check(firstPart) is { Kind: SegmentProblemKind.ReservedDeviceName })
        {
            return FallbackStem + stem[firstPart.Length..];
        }

        return stem;
    }
}
