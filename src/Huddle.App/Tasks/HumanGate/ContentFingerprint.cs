using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>The hash an acceptance is bound to: the Task's title, Action and description (Human gate Spec §6.5, D-5).</summary>
internal static class ContentFingerprint
{
    private const string SectionPrefix = "## ";

    /// <summary>Hashes the content the Human accepted.</summary>
    /// <param name="title">The Task title.</param>
    /// <param name="action">The Action, or <see langword="null"/> when there is none.</param>
    /// <param name="description">The description; its <c>## Evidence</c> section is left out.</param>
    /// <returns>The first twelve lowercase hex characters of SHA-256 over <c>title\naction\ndescription</c>, LF-normalised.</returns>
    public static string Of(string title, string? action, string description)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        string text = string.Concat(title, "\n", action ?? "", "\n", WithoutEvidence(description))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];
    }

    /// <summary>Removes the <c>## Evidence</c> section and trims the end of what remains.</summary>
    /// <param name="description">The description.</param>
    private static string WithoutEvidence(string description)
    {
        string normalized = description.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        List<string> kept = [];
        bool inFence = false;
        bool inEvidence = false;
        foreach (string line in normalized.Split('\n'))
        {
            string trimmed = line.Trim();
            if (inEvidence)
            {
                if (line.StartsWith(SectionPrefix, StringComparison.Ordinal))
                {
                    inEvidence = false;
                }
                else
                {
                    continue;
                }
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && string.Equals(trimmed, TaskFileFormat.EvidenceHeading, StringComparison.OrdinalIgnoreCase))
            {
                inEvidence = true;
                continue;
            }

            kept.Add(line);
        }

        return string.Join('\n', kept).TrimEnd();
    }
}
