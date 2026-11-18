using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Keeps the text of each default Playbook a Team ever customized from, in <c>{DataDir}/playbook-defaults/&lt;kind&gt;-&lt;hash&gt;.md</c>, so the tab can show what
/// changed in a default (Team Playbooks spec §5.2). A base file is written once and never edited; startup deletes the ones no Playbook file names.
/// </summary>
/// <param name="dataDir">The data folder the <c>playbook-defaults</c> folder lives in.</param>
internal sealed class PlaybookBaseStore(string dataDir)
{
    /// <summary>The folder under the data folder that holds the base files.</summary>
    public const string FolderName = "playbook-defaults";

    private const int HashLength = 16;

    /// <summary>Hashes a default: the first 16 lower-case hex characters of the SHA-256 of its text, LF-normalised and trimmed.</summary>
    /// <param name="defaultText">The text of a default Playbook.</param>
    /// <returns>The hash a Playbook file names in its <c>basedOn</c>.</returns>
    public static string Hash(string defaultText)
    {
        ArgumentNullException.ThrowIfNull(defaultText);

        string normalised = defaultText.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)))[..HashLength];
    }

    /// <summary>Stores a default's text under its hash, unless a base with that hash is stored already.</summary>
    /// <param name="kind">Which Playbook the default is for.</param>
    /// <param name="defaultText">The default's text.</param>
    /// <returns>The hash of the text.</returns>
    public string Save(PlaybookKind kind, string defaultText)
    {
        string hash = Hash(defaultText);
        string path = this.PathFor(kind, hash);
        if (!File.Exists(path))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dataDir);
            AtomicFile.WriteText(path, defaultText);
        }

        return hash;
    }

    /// <summary>Reads a stored base.</summary>
    /// <param name="kind">Which Playbook the base is for.</param>
    /// <param name="hash">The base's hash.</param>
    /// <returns>The text, or <see langword="null"/> when there is no such base or it cannot be read.</returns>
    public string? Read(PlaybookKind kind, string hash)
    {
        string path = this.PathFor(kind, hash);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The path a base is stored at.</summary>
    /// <param name="kind">Which Playbook the base is for.</param>
    /// <param name="hash">The base's hash.</param>
    /// <returns><c>{dataDir}/playbook-defaults/member-&lt;hash&gt;.md</c> or the <c>manager-</c> one.</returns>
    public string PathFor(PlaybookKind kind, string hash) =>
        Path.Combine(dataDir, FolderName, $"{KindName(kind)}-{hash}.md");

    /// <summary>The folder the base files live in.</summary>
    /// <returns>The full folder path, whether or not it exists.</returns>
    public string Folder() => Path.Combine(dataDir, FolderName);

    /// <summary>The word a kind goes by in a base file's name.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><c>member</c> or <c>manager</c>.</returns>
    public static string KindName(PlaybookKind kind) => kind == PlaybookKind.Manager ? "manager" : "member";
}
