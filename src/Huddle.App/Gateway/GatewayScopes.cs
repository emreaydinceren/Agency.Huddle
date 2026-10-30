namespace Agency.Huddle.App.Gateway;

/// <summary>The operator scopes (Spec 6.7) and the set arithmetic over them.</summary>
internal static class GatewayScopes
{
    /// <summary>See.</summary>
    public const string Read = "operator.read";

    /// <summary>Use the app.</summary>
    public const string Write = "operator.write";

    /// <summary>Approve and reject devices with scopes the approver holds.</summary>
    public const string Pairing = "operator.pairing";

    /// <summary>Everything; implies every scope.</summary>
    public const string Admin = "operator.admin";

    private static readonly string[] LimitedSet = [Read, Write];

    private static readonly string[] AdminScopes = [Admin, Pairing, Read, Write];

    /// <summary>The scopes of a limited device: read and write.</summary>
    public static IReadOnlyList<string> Limited => LimitedSet;

    /// <summary>The scopes of an admin device: every scope.</summary>
    public static IReadOnlyList<string> AdminSet => AdminScopes;

    /// <summary>Distinct, ordinally sorted scopes; <see cref="Write"/> brings <see cref="Read"/> with it (Spec 6.7).</summary>
    /// <param name="scopes">The scopes.</param>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        HashSet<string> set = new(scopes, StringComparer.Ordinal);
        if (set.Contains(Write))
        {
            _ = set.Add(Read);
        }

        string[] sorted = [.. set];
        Array.Sort(sorted, StringComparer.Ordinal);
        return sorted;
    }

    /// <summary>The sorted scopes joined by commas, as stored in <c>gateway_devices.scopes</c> and signed in the payload.</summary>
    /// <param name="scopes">The scopes.</param>
    public static string Join(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        string[] sorted = [.. scopes];
        Array.Sort(sorted, StringComparer.Ordinal);
        return string.Join(',', sorted);
    }

    /// <summary>Whether <paramref name="held"/> includes <paramref name="scope"/>; <see cref="Admin"/> holds every scope.</summary>
    /// <param name="held">The scopes held.</param>
    /// <param name="scope">The scope wanted.</param>
    public static bool Holds(IEnumerable<string> held, string scope)
    {
        ArgumentNullException.ThrowIfNull(held);

        foreach (string candidate in held)
        {
            if (string.Equals(candidate, scope, StringComparison.Ordinal) || string.Equals(candidate, Admin, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="held"/> holds every scope in <paramref name="wanted"/>.</summary>
    /// <param name="held">The scopes held.</param>
    /// <param name="wanted">The scopes wanted.</param>
    public static bool HoldsAll(IEnumerable<string> held, IEnumerable<string> wanted)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(wanted);

        string[] heldScopes = [.. held];
        return wanted.All(scope => Holds(heldScopes, scope));
    }

    /// <summary>Keeps only the four known scopes, normalised; empty when none is known.</summary>
    /// <param name="scopes">The scopes as presented.</param>
    public static IReadOnlyList<string> Known(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        return Normalize(scopes.Where(scope => scope is Read or Write or Pairing or Admin));
    }
}
