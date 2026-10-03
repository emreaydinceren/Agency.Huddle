namespace Agency.Huddle.AnalyzerProbes;

// The one probe that needs a high-entropy string literal. gitleaks flags it as a generic API key, so
// this file is allowlisted by path in .gitleaks.toml; keep nothing else in it and never put a real
// secret here. Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.

public class SecretProbes
{
    public string Secret() { /* probe: S6418 */ const string apiKey = "9f8d7c6b5a4e3d2c1b0a9f8e7d6c5b4a3f2e1d0c"; return apiKey; }
}
