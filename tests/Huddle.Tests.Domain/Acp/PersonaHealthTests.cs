using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="PersonaHealth"/>.</summary>
public sealed class PersonaHealthTests
{
    /// <summary>A hand-written, controllable <see cref="TimeProvider"/> so a test can move time without racing the real clock.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now;

        /// <summary>Creates a clock fixed at <paramref name="start"/>.</summary>
        /// <param name="start">The instant this clock reports until <see cref="Advance"/> is called.</param>
        public ManualTimeProvider(DateTimeOffset start)
        {
            this.now = start;
        }

        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow()
        {
            return this.now;
        }

        /// <summary>Moves this clock forward by <paramref name="delta"/>.</summary>
        /// <param name="delta">How far forward to move.</param>
        public void Advance(TimeSpan delta)
        {
            this.now += delta;
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A report that changes the recorded state raises Changed.</summary>
    [Fact]
    public void Report_RaisesChanged_WhenTheStateDiffers()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        var raised = false;
        health.Changed += () => raised = true;

        health.Report("Jarvis", PersonaState.Online, null);

        Assert.True(raised);
    }

    /// <summary>Reporting the exact same state and reason again raises nothing — a repeated Online after every Turn must not repaint every tile.</summary>
    [Fact]
    public void Report_DoesNotRaiseChanged_WhenNothingDiffers()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        health.Report("Jarvis", PersonaState.Online, null);
        var raised = false;
        health.Changed += () => raised = true;

        health.Report("Jarvis", PersonaState.Online, null);

        Assert.False(raised);
    }

    /// <summary>A no-op report leaves Since exactly where it was, because Since means "since when has it been like this".</summary>
    [Fact]
    public void Report_DoesNotResetSince_WhenNothingDiffers()
    {
        ManualTimeProvider clock = new(PersonaHealthTests.Start);
        PersonaHealth health = new(clock, NullLogger<PersonaHealth>.Instance);
        health.Report("Jarvis", PersonaState.Online, null);
        PersonaStatus? before = health.Get("Jarvis");
        Assert.NotNull(before);

        clock.Advance(TimeSpan.FromMinutes(5));
        health.Report("Jarvis", PersonaState.Online, null);

        PersonaStatus? after = health.Get("Jarvis");
        Assert.NotNull(after);
        Assert.Equal(before.Since, after.Since);
    }

    /// <summary>A Persona's Name is looked up case-insensitively, matching MentionParser and persona_models.</summary>
    [Fact]
    public void Get_IsCaseInsensitive()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        health.Report("Jarvis", PersonaState.Online, null);

        PersonaStatus? status = health.Get("jarvis");

        Assert.NotNull(status);
        Assert.Equal(PersonaState.Online, status.State);
    }

    /// <summary>Removing a Persona's status drops it entirely — a later Get finds nothing.</summary>
    [Fact]
    public void Remove_DropsTheEntry()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        health.Report("Jarvis", PersonaState.Online, null);

        health.Remove("Jarvis");

        Assert.Null(health.Get("Jarvis"));
    }

    /// <summary>One Changed subscriber throwing must not stop a later subscriber from running, nor break the caller of Report.</summary>
    [Fact]
    public void AThrowingSubscriber_DoesNotStopTheOthers()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        var secondRan = false;
        health.Changed += () => throw new InvalidOperationException("Deliberate failure from a test subscriber.");
        health.Changed += () => secondRan = true;

        health.Report("Jarvis", PersonaState.Online, null);

        Assert.True(secondRan);
    }

    /// <summary>All returns a copy: a report made after reading it never changes the dictionary already handed back.</summary>
    [Fact]
    public void All_ReturnsASnapshot_NotALiveView()
    {
        PersonaHealth health = new(new ManualTimeProvider(PersonaHealthTests.Start), NullLogger<PersonaHealth>.Instance);
        health.Report("Jarvis", PersonaState.Online, null);

        var snapshot = health.All;
        health.Report("Emily", PersonaState.Online, null);

        Assert.Single(snapshot);
    }
}
