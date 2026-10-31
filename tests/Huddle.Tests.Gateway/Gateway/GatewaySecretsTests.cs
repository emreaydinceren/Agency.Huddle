using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.1 and 8.2: generated secrets, the PBKDF2 gateway password and constant-time comparison.</summary>
public sealed partial class GatewaySecretsTests
{
    [GeneratedRegex("^[0-9a-f]{48}$", RegexOptions.CultureInvariant)]
    private static partial Regex Token48Hex();

    [GeneratedRegex("^[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceId8Hex();

    /// <summary>A gateway token is 24 random bytes written as 48 lowercase hex characters.</summary>
    [Fact]
    public void GenerateToken_Is48LowercaseHex()
    {
        string token = GatewaySecrets.GenerateToken();

        Assert.Matches(Token48Hex(), token);
    }

    /// <summary>Two generated tokens differ, so the token really is random.</summary>
    [Fact]
    public void GenerateToken_TwoCalls_Differ()
    {
        string first = GatewaySecrets.GenerateToken();
        string second = GatewaySecrets.GenerateToken();

        Assert.NotEqual(first, second);
    }

    /// <summary>An Instance id is 8 lowercase hex characters.</summary>
    [Fact]
    public void GenerateInstanceId_Is8LowercaseHex()
    {
        string id = GatewaySecrets.GenerateInstanceId();

        Assert.Matches(InstanceId8Hex(), id);
    }

    /// <summary>The stored password is a 16-byte salt followed by PBKDF2-SHA256 at 600,000 iterations.</summary>
    [Fact]
    public void HashPassword_IsSixteenByteSaltThenPbkdf2Sha256()
    {
        byte[] stored = GatewaySecrets.HashPassword("correct horse");

        Assert.Equal(16 + 32, stored.Length);
        byte[] salt = stored[..16];
        byte[] expected = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes("correct horse"), salt, 600_000, HashAlgorithmName.SHA256, 32);
        Assert.Equal(expected, stored[16..]);
    }

    /// <summary>Hashing the same password twice gives different bytes, because each hash has its own random salt.</summary>
    [Fact]
    public void HashPassword_SamePasswordTwice_DiffersBySalt()
    {
        byte[] first = GatewaySecrets.HashPassword("same");
        byte[] second = GatewaySecrets.HashPassword("same");

        Assert.NotEqual(first[..16], second[..16]);
    }

    /// <summary>The hash never contains the clear password.</summary>
    [Fact]
    public void HashPassword_DoesNotContainClearText()
    {
        byte[] stored = GatewaySecrets.HashPassword("hunter2hunter2");

        Assert.False(stored.AsSpan().IndexOf(Encoding.UTF8.GetBytes("hunter2hunter2")) >= 0);
    }

    /// <summary>The right password verifies against its hash and a wrong one does not.</summary>
    [Fact]
    public void VerifyPassword_RightAndWrong()
    {
        byte[] stored = GatewaySecrets.HashPassword("open sesame");

        Assert.True(GatewaySecrets.VerifyPassword("open sesame", stored));
        Assert.False(GatewaySecrets.VerifyPassword("open sesame!", stored));
    }

    /// <summary>A stored value of the wrong length is not a hash and never verifies.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(47)]
    [InlineData(49)]
    public void VerifyPassword_MalformedHash_ReturnsFalse(int length)
    {
        Assert.False(GatewaySecrets.VerifyPassword("anything", new byte[length]));
    }

    /// <summary>Equal secrets compare equal; different secrets, including a different length or a different case, do not.</summary>
    [Theory]
    [InlineData("abc123", "abc123", true)]
    [InlineData("abc123", "abc124", false)]
    [InlineData("abc123", "abc12", false)]
    [InlineData("abc123", "ABC123", false)]
    [InlineData("", "", true)]
    public void SecretsEqual_ComparesExactly(string left, string right, bool expected)
    {
        Assert.Equal(expected, GatewaySecrets.SecretsEqual(left, right));
    }
}
