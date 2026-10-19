namespace Agency.Huddle.App.SkillSources;

/// <summary>The move until task P16 writes it: it never runs.</summary>
internal sealed class NoSkillSourcesMove : ISkillSourcesMove
{
    /// <inheritdoc/>
    public Task RunAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
