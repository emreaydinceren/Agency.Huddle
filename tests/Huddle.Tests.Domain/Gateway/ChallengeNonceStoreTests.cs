using System.Buffers.Text;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.6 step 1 and 7.2: nonces are issued here, single use, and good for 60 seconds.</summary>
public sealed class ChallengeNonceStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A challenge is 32 random bytes as base64url, with the server time in milliseconds.</summary>
    [Fact]
    public void Issue_Returns32ByteBase64UrlNonceAndServerTime()
    {
        ChallengeNonceStore store = new(new FakeTimeProvider(Start));

        ChallengeNonceStore.Challenge challenge = store.Issue();

        Assert.Equal(32, Base64Url.DecodeFromChars(challenge.Nonce).Length);
        Assert.Equal(Start.ToUnixTimeMilliseconds(), challenge.Ts);
    }

    /// <summary>Two challenges never share a nonce.</summary>
    [Fact]
    public void Issue_TwiceInARow_GivesDifferentNonces()
    {
        ChallengeNonceStore store = new(new FakeTimeProvider(Start));

        Assert.NotEqual(store.Issue().Nonce, store.Issue().Nonce);
    }

    /// <summary>A fresh nonce is accepted once; presenting it again is a mismatch.</summary>
    [Fact]
    public void Consume_SecondUse_IsMismatch()
    {
        ChallengeNonceStore store = new(new FakeTimeProvider(Start));
        string nonce = store.Issue().Nonce;

        Assert.Equal(ChallengeNonceStore.Check.Valid, store.Consume(nonce));
        Assert.Equal(ChallengeNonceStore.Check.Mismatch, store.Consume(nonce));
    }

    /// <summary>A nonce the gateway never issued is a mismatch.</summary>
    [Fact]
    public void Consume_UnknownNonce_IsMismatch()
    {
        ChallengeNonceStore store = new(new FakeTimeProvider(Start));

        Assert.Equal(ChallengeNonceStore.Check.Mismatch, store.Consume("bm90LWlzc3VlZA"));
    }

    /// <summary>No nonce at all is "required", not a mismatch.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Consume_NoNonce_IsRequired(string? nonce)
    {
        ChallengeNonceStore store = new(new FakeTimeProvider(Start));

        Assert.Equal(ChallengeNonceStore.Check.Required, store.Consume(nonce));
    }

    /// <summary>A nonce is good at 59 seconds and dead after 61.</summary>
    [Fact]
    public void Consume_Expiry_Is60Seconds()
    {
        FakeTimeProvider clock = new(Start);
        ChallengeNonceStore store = new(clock);
        string fresh = store.Issue().Nonce;
        string stale = store.Issue().Nonce;

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(ChallengeNonceStore.Check.Valid, store.Consume(fresh));
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(ChallengeNonceStore.Check.Mismatch, store.Consume(stale));
    }
}
