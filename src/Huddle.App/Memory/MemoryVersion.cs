using System.Security.Cryptography;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The version token of a memory file (Instructions spec §5.4): the first 12 hex characters of the SHA-256 of its bytes.
/// Computed from what is on disk, so an edit made in the Library or with an Agent's own <c>Write</c> changes it too.
/// </summary>
internal static class MemoryVersion
{
    /// <summary>The number of hex characters in a token.</summary>
    private const int TokenLength = 12;

    /// <summary>The token for <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The file's whole content.</param>
    /// <returns>Twelve lower-case hex characters.</returns>
    public static string Of(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Convert.ToHexStringLower(SHA256.HashData(bytes))[..TokenLength];
    }
}
