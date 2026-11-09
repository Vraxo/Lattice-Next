using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record KnowledgeSource(
    KnowledgeSourceKind Kind,
    string SourceText,
    SemanticIdentifier? ResolvedReferent = null);