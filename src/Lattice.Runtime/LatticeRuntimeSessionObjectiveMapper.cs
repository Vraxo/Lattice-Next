using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Intelligence;
using Lattice.Core.Reasoning;

namespace Lattice.Runtime;

internal static class LatticeRuntimeSessionObjectiveMapper
{
    public static LatticeObjectiveEvaluation MapObjectiveEvaluation(ObjectiveEvaluation evaluation) =>
        new(evaluation.Candidates.Select(candidate => new LatticeCandidateAssessment(
            candidate.Candidate.Value,
            candidate.HasHardConstraintViolation,
            candidate.HasUnresolvedHardConstraint,
            candidate.Conditions.Select(condition => new LatticeObjectiveConditionAssessment(
                condition.ConditionName,
                condition.Role.ToString(),
                condition.Status.ToString(),
                condition.Evidence
                    .SelectMany(attribute => attribute.Sources)
                    .Distinct()
                    .Select(source => new LatticeObjectiveEvidenceSource(
                        source.Kind.ToString(),
                        source.SourceText,
                        source.SourceId,
                        source.RetrievedAt,
                        source.FreshUntil,
                        source.RequiresFreshness))
                    .ToImmutableArray())
            {
                TemporalWitness = condition.TemporalWitness.Select(stage => new LatticeObjectiveTemporalStage(
                    stage.Index,
                    stage.DurationAttribute,
                    stage.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                    stage.End.ToString("HH:mm", CultureInfo.InvariantCulture),
                    stage.DurationMinutes))
                    .ToImmutableArray(),
                Preferences = condition.Preferences.Select(preference => new LatticeObjectivePreferenceAssessment(
                    preference.Preference,
                    preference.Status.ToString(),
                    preference.Evidence
                        .SelectMany(attribute => attribute.Sources)
                        .Distinct()
                        .Select(source => new LatticeObjectiveEvidenceSource(
                            source.Kind.ToString(),
                            source.SourceText,
                            source.SourceId,
                            source.RetrievedAt,
                            source.FreshUntil,
                            source.RequiresFreshness))
                        .ToImmutableArray()))
                    .ToImmutableArray()
            }
    private static ImmutableArray<LatticeObjectiveEvidenceSource> MapObjectiveEvidenceSources(
        IEnumerable<KnowledgeAttribute> attributes) =>
        attributes
            .SelectMany(attribute => attribute.Sources)
            .Distinct()
            .Select(source => new LatticeObjectiveEvidenceSource(
                source.Kind.ToString(),
                source.SourceText,
                source.SourceId,
                source.RetrievedAt,
                source.FreshUntil,
                source.RequiresFreshness))
            .ToImmutableArray();

    private static LatticeEvidence MapEvidence(ProofNode proof)
    {
        var premises = ImmutableArray.CreateBuilder<LatticeEvidence>();

        if (proof.Premise is not null)
        {
            premises.Add(MapEvidence(proof.Premise));
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var premise in proof.AdditionalPremises)
            {
                premises.Add(MapEvidence(premise));
            }
        }

        return new LatticeEvidence(
            proof.Kind switch
            {
                ProofNodeKind.Asserted => LatticeEvidenceKind.Asserted,
                _ => LatticeEvidenceKind.Derived
            },
            proof.Polarity switch
            {
                EvidencePolarity.Positive => LatticeEvidencePolarity.Positive,
                _ => LatticeEvidencePolarity.Negative
            },
            proof.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    _ => LatticeKnowledgeSourceKind.ExternalObservation
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value)).ToImmutableArray(),
            premises.ToImmutable());
    }
    public static LatticeEvidence MapEvidence(ProofNode proof)
    {
        var premises = ImmutableArray.CreateBuilder<LatticeEvidence>();

        if (proof.Premise is not null)
        {
            premises.Add(MapEvidence(proof.Premise));
        }

        if (!proof.AdditionalPremises.IsDefaultOrEmpty)
        {
            foreach (var premise in proof.AdditionalPremises)
            {
                premises.Add(MapEvidence(premise));
            }
        }

        return new LatticeEvidence(
            proof.Kind switch
            {
                ProofNodeKind.Asserted => LatticeEvidenceKind.Asserted,
                _ => LatticeEvidenceKind.Derived
            },
            proof.Polarity switch
            {
                EvidencePolarity.Positive => LatticeEvidencePolarity.Positive,
                _ => LatticeEvidencePolarity.Negative
            },
            proof.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    _ => LatticeKnowledgeSourceKind.ExternalObservation
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value)).ToImmutableArray(),
            premises.ToImmutable());
    }
}
