// Block-scoped namespaces: these probes cannot share a file with a file-scoped namespace.

// probe: S3261
namespace Agency.Huddle.AnalyzerProbes.EmptyNamespace
{
}

// probe: S3903
internal sealed class GlobalNamespaceType
{
}
