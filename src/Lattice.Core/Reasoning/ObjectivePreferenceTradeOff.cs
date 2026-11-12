using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

/// <summary>An evidence-backed preference distinction between two eligible candidates.</summary>
public sealed record ObjectivePreferenceTradeOff(
    string Preference,
    SemanticIdentifier CandidateWithPositiveEvidence,
    SemanticIdentifier CandidateWithContraryEvidence,
    ImmutableArray<KnowledgeAttribute> PositiveEvidence,
    ImmutableArray<KnowledgeAttribute> ContraryEvidence);
