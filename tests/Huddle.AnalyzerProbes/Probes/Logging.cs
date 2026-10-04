using Microsoft.Extensions.Logging;

namespace Agency.Huddle.AnalyzerProbes;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public class LoggingA
{
    private readonly ILogger<string> wrongType;

    // probe: S6672
    public LoggingA(ILogger<string> logger) { this.wrongType = logger; }

    public void Interpolated(ILogger logger, int n) { /* probe: S2629 */ logger.LogInformation($"n={n}"); }

    public void WrongParameter(ILogger logger, Exception ex) { /* probe: S6668 */ logger.LogError("failed {Error}", ex); }

    public void Order(ILogger logger, int second, int first) { /* probe: S6673 */ logger.LogInformation("{First} {Second}", second, first); }

    public void BadTemplate(ILogger logger) { /* probe: S6674 */ logger.LogInformation("value {"); }

    public void Duplicates(ILogger logger, int a, int b) { /* probe: S6677 */ logger.LogInformation("{Item} {Item}", a, b); }

    public void CamelCase(ILogger logger, int a) { /* probe: S6678 */ logger.LogInformation("{itemCount}", a); }
}
