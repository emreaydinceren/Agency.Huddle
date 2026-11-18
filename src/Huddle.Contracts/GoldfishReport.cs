namespace Agency.Huddle.Contracts;

/// <summary>What a runner reports about a goldfish possession. Client to server.</summary>
/// <param name="PossessionId">The possession's id.</param>
/// <param name="Kind">What happened.</param>
/// <param name="RunningSpend">The spend so far, when known.</param>
/// <param name="Currency">The currency of <paramref name="RunningSpend"/>.</param>
/// <param name="Detail">A short explanation, for a failure.</param>
/// <param name="Done">Whether the Agent finished the brief; set only with <see cref="GoldfishReportKind.TurnEnded"/>.</param>
public sealed record GoldfishReport(
    string PossessionId, GoldfishReportKind Kind, decimal? RunningSpend = null, string? Currency = null, string? Detail = null, bool? Done = null)
    : ProtocolMessage;
