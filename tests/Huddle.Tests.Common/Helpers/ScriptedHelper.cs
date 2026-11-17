using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>The Verdict of <see cref="ScriptedHelper"/>; <c>yes</c> is required, so a missing property is not a quiet false.</summary>
/// <param name="Yes">The answer.</param>
/// <param name="Why">Why.</param>
internal sealed record ScriptedVerdict([property: JsonRequired] bool Yes, string? Why);

/// <summary>Source-generated JSON metadata for <see cref="ScriptedVerdict"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ScriptedVerdict))]
internal sealed partial class ScriptedVerdictJson : JsonSerializerContext;

/// <summary>A Helper whose behaviour a test scripts: it judges a string and records every apply.</summary>
internal sealed class ScriptedHelper : IHelperAgent<string, ScriptedVerdict>
{
    private readonly List<(string Key, string Input, ScriptedVerdict Verdict)> applied = [];

    /// <summary>Creates the Helper.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="defaults">Its code defaults.</param>
    public ScriptedHelper(string id = "Scripted", HelperDefaults? defaults = null)
    {
        this.Id = new HelperId(id);
        this.Defaults = defaults ?? new HelperDefaults(true, null, null, null, 50, 10, 10, 60);
    }

    /// <inheritdoc/>
    public HelperId Id { get; }

    /// <inheritdoc/>
    public string DisplayName => "Scripted";

    /// <inheritdoc/>
    public string Description => "A scripted Helper.";

    /// <inheritdoc/>
    public IReadOnlyList<string> PromptKeys => ["test.system", "test.task"];

    /// <inheritdoc/>
    public HelperDefaults Defaults { get; }

    /// <summary>Whether the invoker may reuse its Verdicts; a test sets it to <see langword="false"/> to prove the opt-out.</summary>
    public bool CachesVerdicts { get; set; } = true;

    /// <inheritdoc/>
    public JsonTypeInfo<ScriptedVerdict> VerdictType => ScriptedVerdictJson.Default.ScriptedVerdict;

    /// <summary>What <see cref="ApplyAsync"/> answers; reassign per test.</summary>
    public Func<string, string, ScriptedVerdict, HelperApplied> ApplyHandler { get; set; } = static (_, _, _) => new HelperApplied.NoChange();

    /// <summary>Every apply, in order.</summary>
    public IReadOnlyList<(string Key, string Input, ScriptedVerdict Verdict)> Applied
    {
        get
        {
            lock (this.applied)
            {
                return [.. this.applied];
            }
        }
    }

    /// <inheritdoc/>
    public string Fingerprint(string input) => input;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PromptValues(string input) => new Dictionary<string, string>(StringComparer.Ordinal) { ["{{input}}"] = input };

    /// <inheritdoc/>
    public string? Validate(ScriptedVerdict verdict) => string.Equals(verdict.Why, "bad", StringComparison.Ordinal) ? "the reason was bad" : null;

    /// <inheritdoc/>
    public Task<HelperApplied> ApplyAsync(string key, string input, ScriptedVerdict verdict, CancellationToken cancellationToken)
    {
        lock (this.applied)
        {
            this.applied.Add((key, input, verdict));
        }

        return Task.FromResult(this.ApplyHandler(key, input, verdict));
    }
}
