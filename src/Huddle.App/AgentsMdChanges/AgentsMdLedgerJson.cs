using System.Text.Json.Serialization;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>The source-generated JSON context of the ledger file (Spec §7.1).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AgentsMdLedgerDocument))]
internal sealed partial class AgentsMdLedgerJson : JsonSerializerContext;
