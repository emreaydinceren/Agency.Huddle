using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.5: OpenClaw's <c>auth-rate-limit.ts</c> as Huddle builds it, driven by a <see cref="FakeTimeProvider"/>.</summary>
public sealed class AuthRateLimiterTests
{
    private const string Client = "203.0.113.9";

    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static string Secret(string client = Client) => AuthRateLimiter.Key(AuthRateLimitScopes.SharedSecret, client);

    /// <summary>Creates a limiter with the Appendix A defaults (10 failures in 60 s, 5 minute lockout, loopback exempt).</summary>
    private static AuthRateLimiter Create(FakeTimeProvider clock, Action<AuthRateLimitOptions>? configure = null)
    {
        AuthRateLimitOptions options = new() { MaxAttempts = 10, WindowSeconds = 60, LockoutSeconds = 300, ExemptLoopback = true };
        configure?.Invoke(options);
        return new AuthRateLimiter(options, clock);
    }

    /// <summary>Records <paramref name="count"/> failures for the key, <paramref name="gap"/> apart, and returns the last outcome.</summary>
    private static AuthRateLimitFailure Fail(AuthRateLimiter limiter, FakeTimeProvider clock, string key, int count, TimeSpan gap = default, bool loopback = false)
    {
        AuthRateLimitFailure last = default;
        for (int index = 0; index < count; index++)
        {
            last = limiter.RecordFailure(key, loopback);
            if (index < count - 1)
            {
                clock.Advance(gap);
            }
        }

        return last;
    }

    /// <summary>A key is the scope and the address joined by a colon.</summary>
    [Fact]
    public void Key_JoinsScopeAndAddress()
    {
        Assert.Equal("shared-secret:203.0.113.9", AuthRateLimiter.Key(AuthRateLimitScopes.SharedSecret, Client));
        Assert.Equal("pairing:10.0.0.1", AuthRateLimiter.Key(AuthRateLimitScopes.Pairing, "10.0.0.1"));
    }

    /// <summary>A browser-origin key is the fixed prefix and the origin.</summary>
    [Fact]
    public void BrowserOriginKey_UsesTheBrowserOriginPrefix()
    {
        Assert.Equal("browser-origin:https://evil.example", AuthRateLimiter.BrowserOriginKey("https://evil.example"));
    }

    /// <summary>A key with no failures may attempt, with nothing to wait for.</summary>
    [Fact]
    public void Check_NoFailures_IsAllowed()
    {
        AuthRateLimiter limiter = Create(new FakeTimeProvider(Start));

        AuthRateLimitDecision decision = limiter.Check(Secret(), loopback: false);

        Assert.True(decision.Allowed);
        Assert.Equal(0, decision.RetryAfterMs);
    }

    /// <summary>Nine failures in the window are not yet a lockout.</summary>
    [Fact]
    public void RecordFailure_NineInWindow_IsNotLockedOut()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);

        AuthRateLimitFailure last = Fail(limiter, clock, Secret(), 9);

        Assert.False(last.LockedOut);
        Assert.True(limiter.Check(Secret(), loopback: false).Allowed);
    }

    /// <summary>The tenth failure in 60 seconds locks the key out for 5 minutes, and says how long.</summary>
    [Fact]
    public void RecordFailure_TenInWindow_LocksOutForFiveMinutes()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);

        AuthRateLimitFailure last = Fail(limiter, clock, Secret(), 10);

        Assert.True(last.LockedOut);
        Assert.Equal(300_000, last.RetryAfterMs);
        AuthRateLimitDecision decision = limiter.Check(Secret(), loopback: false);
        Assert.False(decision.Allowed);
        Assert.Equal(300_000, decision.RetryAfterMs);
    }

    /// <summary><c>retryAfterMs</c> counts down as the lockout runs.</summary>
    [Fact]
    public void Check_DuringLockout_RetryAfterCountsDown()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 10);

        clock.Advance(TimeSpan.FromSeconds(100));

        AuthRateLimitDecision decision = limiter.Check(Secret(), loopback: false);
        Assert.False(decision.Allowed);
        Assert.Equal(200_000, decision.RetryAfterMs);
    }

    /// <summary>When the lockout ends the key may attempt again and starts counting from zero.</summary>
    [Fact]
    public void Check_AfterLockout_IsAllowedAndCountsFromZero()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 10);

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(1));

        Assert.True(limiter.Check(Secret(), loopback: false).Allowed);
        Assert.False(limiter.RecordFailure(Secret(), loopback: false).LockedOut);
    }

    /// <summary>Failures older than the window no longer count: nine, a pause past the window, then one more is not a lockout.</summary>
    [Fact]
    public void RecordFailure_OutsideTheWindow_DoesNotAccumulate()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 9);
        clock.Advance(TimeSpan.FromSeconds(61));

        AuthRateLimitFailure last = limiter.RecordFailure(Secret(), loopback: false);

        Assert.False(last.LockedOut);
        Assert.True(limiter.Check(Secret(), loopback: false).Allowed);
    }

    /// <summary>Ten failures spread across 45 seconds are all inside the window, so they lock out.</summary>
    [Fact]
    public void RecordFailure_SpreadInsideTheWindow_StillLocksOut()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);

        AuthRateLimitFailure last = Fail(limiter, clock, Secret(), 10, TimeSpan.FromSeconds(5));

        Assert.True(last.LockedOut);
    }

    /// <summary>Each scope counts separately: a locked-out shared secret does not lock out device tokens from the same address.</summary>
    [Fact]
    public void Scopes_AreCountedSeparately()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 10);

        Assert.False(limiter.Check(Secret(), loopback: false).Allowed);
        Assert.True(limiter.Check(AuthRateLimiter.Key(AuthRateLimitScopes.DeviceToken, Client), loopback: false).Allowed);
    }

    /// <summary>Each address counts separately.</summary>
    [Fact]
    public void Addresses_AreCountedSeparately()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 10);

        Assert.True(limiter.Check(Secret("203.0.113.10"), loopback: false).Allowed);
    }

    /// <summary>A success resets the key: nine failures, a reset, and nine more are not a lockout.</summary>
    [Fact]
    public void Reset_ClearsTheFailures()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 9);

        limiter.Reset(Secret());
        AuthRateLimitFailure last = Fail(limiter, clock, Secret(), 9);

        Assert.False(last.LockedOut);
    }

    /// <summary>The attempt count, window and lockout come from the options.</summary>
    [Fact]
    public void Options_ChangeAttemptsWindowAndLockout()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock, options =>
        {
            options.MaxAttempts = 3;
            options.WindowSeconds = 10;
            options.LockoutSeconds = 20;
        });

        AuthRateLimitFailure last = Fail(limiter, clock, Secret(), 3);

        Assert.True(last.LockedOut);
        Assert.Equal(20_000, last.RetryAfterMs);
    }

    /// <summary>A non-loopback failure carries no delay.</summary>
    [Fact]
    public void RecordFailure_NonLoopback_HasNoDelay()
    {
        AuthRateLimiter limiter = Create(new FakeTimeProvider(Start));

        AuthRateLimitFailure failure = limiter.RecordFailure(Secret(), loopback: false);

        Assert.Equal(TimeSpan.Zero, failure.Delay);
    }

    /// <summary>A loopback client is slowed progressively: 250 ms, doubling, at most 5 seconds. The delay is computed, never slept.</summary>
    [Fact]
    public void RecordFailure_Loopback_DelayDoublesFrom250msUpTo5s()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        string key = Secret("127.0.0.1");

        List<double> delays = [];
        for (int index = 0; index < 8; index++)
        {
            delays.Add(limiter.RecordFailure(key, loopback: true).Delay.TotalMilliseconds);
        }

        Assert.Equal([250d, 500d, 1000d, 2000d, 4000d, 5000d, 5000d, 5000d], delays);
    }

    /// <summary>A loopback client is never locked out, however many failures, and may always attempt.</summary>
    [Fact]
    public void Loopback_IsNeverLockedOut()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        string key = Secret("127.0.0.1");

        AuthRateLimitFailure last = Fail(limiter, clock, key, 50, loopback: true);

        Assert.False(last.LockedOut);
        AuthRateLimitDecision decision = limiter.Check(key, loopback: true);
        Assert.True(decision.Allowed);
        Assert.Equal(0, decision.RetryAfterMs);
    }

    /// <summary>The loopback delay restarts at 250 ms once the failures have aged out of the window, and after a reset.</summary>
    [Fact]
    public void RecordFailure_Loopback_DelayRestartsAfterWindowAndReset()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        string key = Secret("127.0.0.1");
        Fail(limiter, clock, key, 3, loopback: true);

        clock.Advance(TimeSpan.FromSeconds(61));
        double afterWindow = limiter.RecordFailure(key, loopback: true).Delay.TotalMilliseconds;
        limiter.Reset(key);
        double afterReset = limiter.RecordFailure(key, loopback: true).Delay.TotalMilliseconds;

        Assert.Equal(250d, afterWindow);
        Assert.Equal(250d, afterReset);
    }

    /// <summary>With <c>ExemptLoopback</c> off, a loopback client is locked out like any other.</summary>
    [Fact]
    public void Loopback_WhenExemptionIsOff_IsLockedOut()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock, options => options.ExemptLoopback = false);
        string key = Secret("127.0.0.1");

        AuthRateLimitFailure last = Fail(limiter, clock, key, 10, loopback: true);

        Assert.True(last.LockedOut);
        Assert.False(limiter.Check(key, loopback: true).Allowed);
    }

    /// <summary>A browser-origin key is not exempt for loopback: ten failures lock it out even from a loopback client.</summary>
    [Fact]
    public void BrowserOrigin_LoopbackIsNotExempt()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        string key = AuthRateLimiter.BrowserOriginKey("https://evil.example");

        AuthRateLimitFailure last = Fail(limiter, clock, key, 10, loopback: true);

        Assert.True(last.LockedOut);
        AuthRateLimitDecision decision = limiter.Check(key, loopback: true);
        Assert.False(decision.Allowed);
        Assert.Equal(300_000, decision.RetryAfterMs);
    }

    /// <summary>At most 10,000 keys are tracked: past that the limiter prunes instead of growing.</summary>
    [Fact]
    public void RecordFailure_BeyondTheCap_KeepsAtMostTenThousandEntries()
    {
        AuthRateLimiter limiter = Create(new FakeTimeProvider(Start));

        for (int index = 0; index < 10_500; index++)
        {
            limiter.RecordFailure(Secret("198.51.100." + index), loopback: false);
        }

        Assert.InRange(limiter.Count, 1, 10_000);
    }

    /// <summary>Pruning removes keys whose window has passed.</summary>
    [Fact]
    public void Prune_RemovesKeysWhoseWindowHasPassed()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret("198.51.100.1"), 1);
        Fail(limiter, clock, Secret("198.51.100.2"), 1);
        Fail(limiter, clock, Secret("198.51.100.3"), 1);
        Assert.Equal(3, limiter.Count);

        clock.Advance(TimeSpan.FromSeconds(61));
        limiter.Prune();

        Assert.Equal(0, limiter.Count);
    }

    /// <summary>Pruning keeps a locked-out key until its lockout has ended, then drops it.</summary>
    [Fact]
    public void Prune_KeepsLockedOutKeyUntilLockoutEnds()
    {
        FakeTimeProvider clock = new(Start);
        AuthRateLimiter limiter = Create(clock);
        Fail(limiter, clock, Secret(), 10);

        clock.Advance(TimeSpan.FromSeconds(61));
        limiter.Prune();
        Assert.Equal(1, limiter.Count);
        Assert.False(limiter.Check(Secret(), loopback: false).Allowed);

        clock.Advance(TimeSpan.FromMinutes(5));
        limiter.Prune();
        Assert.Equal(0, limiter.Count);
    }
}
