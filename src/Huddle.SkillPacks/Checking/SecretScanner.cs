using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>The ten X001 patterns of §6.3. Every regex has a 1-second timeout; a timeout counts as no match.</summary>
public static partial class SecretScanner
{
    /// <summary>The largest file scanned.</summary>
    public const int MaxBytes = 1024 * 1024;

    private const int BinaryProbe = 8192;

    private static readonly (int Number, string Name, Func<Regex> Regex)[] Patterns =
    [
        (1, "GitHub token", GitHubToken),
        (2, "GitHub fine-grained token", GitHubFineGrained),
        (3, "GitLab token", GitLabToken),
        (4, "AWS access key id", AwsKey),
        (5, "Slack token", SlackToken),
        (6, "API key with a vendor prefix", VendorKey),
        (7, "Private key header", PrivateKey),
        (8, "Password line", PasswordLine),
        (9, "Bearer or Basic credential", AuthorizationHeader),
        (10, "Credentials in a URL", UrlCredentials),
    ];

    /// <summary>Whether <paramref name="bytes"/> is binary: a NUL in its first 8 KB.</summary>
    public static bool IsBinary(ReadOnlySpan<byte> bytes)
    {
        return bytes[..Math.Min(bytes.Length, BinaryProbe)].Contains((byte)0);
    }

    /// <summary>Every hit in <paramref name="text"/>, in pattern then line order, at most one per pattern and line.</summary>
    public static IReadOnlyList<SecretHit> Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<SecretHit> hits = [];
        foreach ((int number, string name, Func<Regex> regex) in Patterns)
        {
            MatchCollection matches;
            try
            {
                matches = regex().Matches(text);
                foreach (Match match in matches)
                {
                    int line = LineOf(text, match.Index);
                    if (!hits.Exists(hit => hit.Pattern == number && hit.Line == line))
                    {
                        hits.Add(new SecretHit(number, name, line, Preview(match.Value)));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A timeout counts as no match (§6.3): a pathological file must not stall the checks.
            }
        }

        return hits;
    }

    /// <summary>The finding message for a hit: <c>GitHub token (pattern 1) at line 12: ghp_…</c>.</summary>
    public static string Describe(SecretHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        return string.Create(CultureInfo.InvariantCulture, $"{hit.Name} (pattern {hit.Pattern}) at line {hit.Line}: {hit.Preview}");
    }

    /// <summary>At most the first four characters, then an ellipsis.</summary>
    private static string Preview(string value)
    {
        string trimmed = value.TrimStart();
        return (trimmed.Length <= 4 ? trimmed : trimmed[..4]) + "…";
    }

    /// <summary>The 1-based line of <paramref name="index"/>.</summary>
    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\bgithub_pat_[A-Za-z0-9_]{22,}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex GitHubFineGrained();

    [GeneratedRegex(@"\bglpat-[A-Za-z0-9_-]{20,}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex GitLabToken();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"\bxox[abprs]-[A-Za-z0-9-]{10,}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SlackToken();

    [GeneratedRegex(@"\bsk-(?:ant-|proj-)?[A-Za-z0-9_-]{32,}\b", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex VendorKey();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP |ENCRYPTED )?PRIVATE KEY(?: BLOCK)?-----", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"(?im)^\s*[""']?(?:password|passwd|pwd)[""']?\s*[:=]\s*[""']?(?!(?:your|example|changeme|placeholder|xxx|<|\$\{?|\{\{|\*{3}))[^\s""'<>]{4,}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PasswordLine();

    [GeneratedRegex(@"\bAuthorization:\s*(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]{20,}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]{3,}@", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex UrlCredentials();
}
