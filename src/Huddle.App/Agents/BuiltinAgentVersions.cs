using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Agents;

/// <summary>
/// Knows which texts the Chief of Staff file has shipped with, by hash, so start-up can tell an
/// untouched earlier version (safe to update) from a file the Human edited (never overwritten).
/// The hash ignores line endings and the <c>name</c> and <c>alias</c> lines, which the Human may
/// rename, and trailing white space. The list is the embedded resource
/// <c>Builtin/chief-of-staff.hashes.txt</c>, one SHA-256 per line, oldest first, the current
/// version last; <c>agents/scripts/Get-ChiefOfStaffHashes.ps1</c> prints it from git history.
/// </summary>
internal sealed class BuiltinAgentVersions
{
    private const string ListResourceName = "Builtin/chief-of-staff.hashes.txt";

    private readonly HashSet<string> shipped;

    /// <summary>Creates the list from the hashes of every version ever shipped, the current one included.</summary>
    /// <param name="shippedHashes">Normalised SHA-256 hashes, lower-case hex.</param>
    public BuiltinAgentVersions(IEnumerable<string> shippedHashes)
    {
        ArgumentNullException.ThrowIfNull(shippedHashes);
        this.shipped = new HashSet<string>(shippedHashes, StringComparer.Ordinal);
    }

    /// <summary>The list embedded in the assembly.</summary>
    public static BuiltinAgentVersions Embedded { get; } = new(ReadEmbeddedList());

    /// <summary>The hash of the version this build ships.</summary>
    public static string CurrentHash { get; } = Hash(BuiltinAgent.DefaultText);

    /// <summary>The number of distinct versions in the list.</summary>
    public int Count => this.shipped.Count;

    /// <summary>Whether the list holds <paramref name="hash"/>.</summary>
    /// <param name="hash">A hash from <see cref="Hash"/>.</param>
    /// <returns><see langword="true"/> if that version was shipped.</returns>
    public bool Contains(string hash) => this.shipped.Contains(hash);

    /// <summary>
    /// Whether <paramref name="fileText"/> is an earlier shipped version, neither the current one nor
    /// one the Human edited.
    /// </summary>
    /// <param name="fileText">The file's text as read from disk.</param>
    /// <returns><see langword="true"/> if it matches an earlier version.</returns>
    public bool IsEarlierVersion(string fileText)
    {
        var hash = Hash(fileText);
        return !string.Equals(hash, CurrentHash, StringComparison.Ordinal) && this.shipped.Contains(hash);
    }

    /// <summary>SHA-256 of <see cref="Normalise"/>d <paramref name="text"/>, lower-case hex.</summary>
    /// <param name="text">A Chief of Staff file's text.</param>
    /// <returns>The hash.</returns>
    public static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalise(text))));

    /// <summary>
    /// Line endings to <c>\n</c>, the <c>name</c> and <c>alias</c> lines of the front matter removed,
    /// trailing white space trimmed.
    /// </summary>
    /// <param name="text">A Chief of Staff file's text.</param>
    /// <returns>The text the hash is taken over.</returns>
    public static string Normalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        StringBuilder result = new();
        var delimiters = 0;
        foreach (var line in lines)
        {
            if (string.Equals(line.TrimEnd(), "---", StringComparison.Ordinal) && delimiters < 2)
            {
                delimiters++;
            }
            else if (delimiters == 1 && IsNameOrAlias(line))
            {
                continue;
            }

            _ = result.Append(line).Append('\n');
        }

        return result.ToString().TrimEnd();
    }

    private static bool IsNameOrAlias(string line) =>
        line.StartsWith("name:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("alias:", StringComparison.OrdinalIgnoreCase);

    private static List<string> ReadEmbeddedList()
    {
        List<string> hashes = [];
        foreach (var raw in EmbeddedResources.ReadText(typeof(BuiltinAgentVersions).Assembly, ListResourceName).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length >= 64 && !line.StartsWith('#'))
            {
                hashes.Add(line[..64].ToLowerInvariant());
            }
        }

        return hashes;
    }
}
