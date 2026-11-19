using System.Collections.Immutable;
using Lattice.Client;
using Lattice.Core.Generation;

namespace Lattice.Runtime;

internal static class KnowledgeDrivenGenerationResponder
{
    private static readonly Lazy<SequenceKnowledgeBase?> EmbeddedKnowledge = new(LoadKnowledge);

    internal static bool TryGenerate(
        string input,
        out LatticeUtteranceResult? result)
    {
        result = null;
        var knowledge = EmbeddedKnowledge.Value;
        if (knowledge is null ||
            !SequenceInputFactExtractor.TryExtract(
                knowledge,
                input,
                out var facts,
                out _) ||
            facts.IsDefaultOrEmpty)
        {
            return false;
        }

        var request = new KnowledgeDrivenSequenceGenerationRequest(
            ImmutableArray<string>.Empty,
            facts.Select(fact => fact.Id).ToImmutableArray())
        {
            FactValues = facts
        };

        if (!KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge,
            null,
            request,
            out var generated,
            out _))
        {
            return false;
        }

        var meaning = $"semantic-facts({string.Join(", ", facts.Select(fact => fact.Id))})";
        result = new LatticeUtteranceResult(
            LatticeUtteranceResultKind.TaskCompleted,
            generated!.Text,
            meaning,
            SupportingEvidence: ImmutableArray<LatticeEvidence>.Empty,
            OpposingEvidence: ImmutableArray<LatticeEvidence>.Empty);
        return true;
    }

    private static SequenceKnowledgeBase? LoadKnowledge() =>
        SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out _)
            ? knowledge
            : null;
}
