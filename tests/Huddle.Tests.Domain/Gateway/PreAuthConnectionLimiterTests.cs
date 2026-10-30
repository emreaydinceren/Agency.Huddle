using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.5: at most 128 concurrent unauthenticated requests per address; more get 503, and loopback is not exempt.</summary>
public sealed class PreAuthConnectionLimiterTests
{
    /// <summary>Takes <paramref name="count"/> slots for the address and returns the leases.</summary>
    private static List<IDisposable> Fill(PreAuthConnectionLimiter limiter, string client, int count)
    {
        List<IDisposable> leases = [];
        for (int index = 0; index < count; index++)
        {
            IDisposable? lease = limiter.TryAcquire(client);
            Assert.NotNull(lease);
            leases.Add(lease);
        }

        return leases;
    }

    /// <summary>The first 128 requests from an address get a slot and the 129th gets none.</summary>
    [Fact]
    public void TryAcquire_Beyond128_ReturnsNull()
    {
        PreAuthConnectionLimiter limiter = new();

        Fill(limiter, "203.0.113.9", 128);

        Assert.Null(limiter.TryAcquire("203.0.113.9"));
    }

    /// <summary>Releasing a slot lets the next request in.</summary>
    [Fact]
    public void TryAcquire_AfterRelease_ReturnsALease()
    {
        PreAuthConnectionLimiter limiter = new();
        List<IDisposable> leases = Fill(limiter, "203.0.113.9", 128);

        leases[0].Dispose();

        Assert.NotNull(limiter.TryAcquire("203.0.113.9"));
    }

    /// <summary>Disposing one lease twice frees one slot, not two.</summary>
    [Fact]
    public void Lease_DisposedTwice_FreesOneSlot()
    {
        PreAuthConnectionLimiter limiter = new();
        List<IDisposable> leases = Fill(limiter, "203.0.113.9", 128);

        leases[0].Dispose();
        leases[0].Dispose();

        Assert.NotNull(limiter.TryAcquire("203.0.113.9"));
        Assert.Null(limiter.TryAcquire("203.0.113.9"));
    }

    /// <summary>Each address has its own 128.</summary>
    [Fact]
    public void TryAcquire_OtherAddress_IsIndependent()
    {
        PreAuthConnectionLimiter limiter = new();
        Fill(limiter, "203.0.113.9", 128);

        Assert.NotNull(limiter.TryAcquire("203.0.113.10"));
    }

    /// <summary>Loopback is not exempt: its 129th concurrent request is refused too.</summary>
    [Fact]
    public void TryAcquire_Loopback_IsNotExempt()
    {
        PreAuthConnectionLimiter limiter = new();

        Fill(limiter, "127.0.0.1", 128);

        Assert.Null(limiter.TryAcquire("127.0.0.1"));
    }
}
