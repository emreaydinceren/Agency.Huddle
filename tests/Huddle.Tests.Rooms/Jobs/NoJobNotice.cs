using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>A give-up trace writer that writes nothing, for tests that do not look at give-ups.</summary>
internal sealed class NoJobNotice : IJobGaveUpNotice
{
    /// <inheritdoc />
    public Task NoteAsync(Job job, string reason, CancellationToken ct) => Task.CompletedTask;
}
