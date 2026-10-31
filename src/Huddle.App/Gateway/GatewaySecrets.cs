using System.Security.Cryptography;
using System.Text;

namespace Agency.Huddle.App.Gateway;

/// <summary>Secret generation, hashing and comparison (Spec 8.2, 6.1).</summary>
internal static class GatewaySecrets
{
    /// <summary>PBKDF2 iteration count for the stored gateway password.</summary>
    public const int PasswordIterations = 600_000;

    /// <summary>PBKDF2 salt length in bytes.</summary>
    public const int SaltLength = 16;

    /// <summary>PBKDF2-SHA256 output length in bytes.</summary>
    public const int HashLength = 32;

    /// <summary>A new gateway token: 24 random bytes as 48 lowercase hex characters.</summary>
    public static string GenerateToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    /// <summary>A new Instance id: 4 random bytes as 8 lowercase hex characters.</summary>
    public static string GenerateInstanceId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    /// <summary>Hashes a password as salt followed by PBKDF2-SHA256 output (48 bytes).</summary>
    /// <param name="password">The clear password.</param>
    public static byte[] HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] hash = Derive(password, salt);
        byte[] result = new byte[SaltLength + HashLength];
        salt.CopyTo(result, 0);
        hash.CopyTo(result, SaltLength);
        return result;
    }

    /// <summary>Whether <paramref name="password"/> matches a <see cref="HashPassword"/> result; false for a malformed hash.</summary>
    /// <param name="password">The presented password.</param>
    /// <param name="saltAndHash">The stored value.</param>
    public static bool VerifyPassword(string password, byte[] saltAndHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(saltAndHash);

        if (saltAndHash.Length != SaltLength + HashLength)
        {
            return false;
        }

        byte[] actual = Derive(password, saltAndHash[..SaltLength]);
        return CryptographicOperations.FixedTimeEquals(actual, saltAndHash.AsSpan(SaltLength));
    }

    /// <summary>Compares two secrets as SHA-256 hashes in constant time.</summary>
    /// <param name="left">One secret.</param>
    /// <param name="right">The other.</param>
    public static bool SecretsEqual(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        byte[] leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        byte[] rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, PasswordIterations, HashAlgorithmName.SHA256, HashLength);
}
