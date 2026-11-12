using System.Collections.Immutable;

namespace Lattice.Client;

/// <summary>An evidence-backed preference distinction between two eligible candidates.</summary>
public sealed record LatticeObjectivePreferenceTradeOff(
    string Preference,
    string CandidateWithPositiveEvidence,
    string CandidateWithContraryEvidence,
    ImmutableArray<LatticeObjectiveEvidenceSource> PositiveEvidence,
    ImmutableArray<LatticeObjectiveEvidenceSource> ContraryEvidence);
