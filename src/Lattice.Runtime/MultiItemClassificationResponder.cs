using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class MultiItemClassificationResponder
{
    internal static bool TryRespond(
        MultiItemClassificationFormula request,
        KnowledgeBase knowledgeBase,
        ReasoningEngine reasoningEngine,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        ArgumentNullException.ThrowIfNull(reasoningEngine);

        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var classifications = new List<(SemanticIdentifier Item, SemanticIdentifier Category, ImmutableArray<ProofNode> Proofs)>();

        foreach (var item in request.Items)
        {
            var supportedCategories = request.Categories
                .Select(category => new
                {
                    Category = category,
                    Result = reasoningEngine.Ask(
                        knowledgeBase,
                        new PredicateApplication(
                            new SemanticIdentifier($"{category.Value}-{request.ItemType.Value}"),
                            ImmutableArray.Create<SemanticTerm>(new IndividualTerm(item))))
                })
                .Where(candidate => candidate.Result.IsSupported)
                .ToArray();

            if (supportedCategories.Length != 1)
            {
                return false;
            }

            classifications.Add((
                item,
                supportedCategories[0].Category,
                supportedCategories[0].Result.Proofs));
        }

        message = string.Join(
            " ",
            classifications.Select(classification =>
                $"{Capitalize(Display(classification.Item.Value))} is {Indefinite(Display(classification.Category.Value))} {Display(classification.Category.Value)} {Display(request.ItemType.Value)}."));

        evidence = classifications
            .SelectMany(classification => classification.Proofs)
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

    private static string Display(string value) =>
        value.Replace('-', ' ');

    private static string Indefinite(string value) =>
        "aeiou".Contains(char.ToLowerInvariant(value[0])) ? "an" : "a";

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
