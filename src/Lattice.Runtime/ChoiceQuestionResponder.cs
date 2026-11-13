using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class ChoiceQuestionResponder
{
    internal static bool TryRespond(
        ChoiceQuestionFormula question,
        KnowledgeBase knowledgeBase,
        ReasoningEngine reasoningEngine,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(reasoningEngine);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var supportedChoices = question.Choices
            .Select(choice => new
            {
                Choice = choice,
                Result = reasoningEngine.Ask(
                    knowledgeBase,
                    new PredicateApplication(
                        question.Category,
                        ImmutableArray.Create<SemanticTerm>(
                            new IndividualTerm(choice))))
            })
            .Where(candidate => candidate.Result.IsSupported)
            .ToArray();

        if (supportedChoices.Length != 1)
        {
            return false;
        }

        var selected = supportedChoices[0];
        var category = question.Category.Value.Replace('-', ' ');
        var choice = selected.Choice.Value.Replace('-', ' ');
        message = $"{Capitalize(choice)} is a species of {category}.";
        evidence = selected.Result.Proofs
            .Select(MapEvidence)
            .DistinctBy(GetEvidenceKey)
            .ToImmutableArray();
        return true;
    }

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
            proof.Kind == ProofNodeKind.Asserted
                ? LatticeEvidenceKind.Asserted
                : LatticeEvidenceKind.Derived,
            proof.Polarity == EvidencePolarity.Positive
                ? LatticeEvidencePolarity.Positive
                : LatticeEvidencePolarity.Negative,
            proof.Sources.Select(source => new LatticeEvidenceSource(
                source.Kind switch
                {
                    KnowledgeSourceKind.UserAssertion => LatticeKnowledgeSourceKind.UserAssertion,
                    KnowledgeSourceKind.ImportedClaim => LatticeKnowledgeSourceKind.ImportedClaim,
                    KnowledgeSourceKind.ExternalObservation => LatticeKnowledgeSourceKind.ExternalObservation,
                    _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "Unsupported knowledge source kind.")
                },
                source.SourceText,
                source.SourceId,
                source.ResolvedReferent?.Value)).ToImmutableArray(),
            premises.ToImmutable());
    }

    private static string GetEvidenceKey(LatticeEvidence evidence) =>
        $"{evidence.Kind}:{evidence.Polarity}:{string.Join(";", evidence.Sources.Select(source => $"{source.Kind}:{source.SourceId}:{source.SourceText}"))}";

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
