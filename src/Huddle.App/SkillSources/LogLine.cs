namespace Agency.Huddle.App.SkillSources;

/// <summary>One <c>log.jsonl</c> line as its writer names it; <see cref="SourceLog"/> fills <c>at</c>, <c>computer</c> and
/// <c>huddle</c> (§7.2, "sources/&lt;source&gt;/log.jsonl").</summary>
/// <param name="Event">One of the events of §7.2.</param>
/// <param name="Subject"><c>pack:&lt;id&gt;</c>, <c>catalog:&lt;id&gt;</c>, <c>skill:&lt;name&gt;</c> or <c>file:&lt;path&gt;</c>.</param>
/// <param name="Action">A finer label; only <c>moved</c> uses it.</param>
/// <param name="Detail">One sentence for people.</param>
/// <param name="Code">A check code, for <c>row-broken</c>.</param>
/// <param name="Data">Event-specific values.</param>
internal sealed record LogLine(string Event, string Subject, string? Action = null, string? Detail = null, string? Code = null, IReadOnlyDictionary<string, object?>? Data = null);
