using System.Collections.Immutable;
using Lattice.Core.Generation;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CompositionalTaskParser
{

    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (!PythonProgramRequestParser.TryGetCreationClause(source, out _))
        {
            return false;
        }

        var formula = new TaskRequestFormula(
            TaskRequestAction.Code,
            "python-program",
            "python",
            "python-source",
            "write-standard-output",
            ImmutableArray.Create("text-output"));

        if (PythonFunctionProgramParser.TryGetFunctionCompositionProgram(source, out var compositionSteps))
        {
            formula = formula with { GenerationSteps = compositionSteps };
        }
        else if (!PythonProgramRequestParser.HasOutputIntent(source) && !CanGenerateDeclaratively(source))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }
    private static bool CanGenerateDeclaratively(string source)
    {
        if (!SequenceKnowledgeBaseLoader.TryLoadEmbedded(out var knowledge, out _) ||
            !SequenceInputFactExtractor.TryExtract(knowledge!, source, out var facts, out _) ||
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

        return KnowledgeDrivenSequenceGenerator.TryGenerate(
            knowledge!,
            null,
            request,
            out _,
            out _);
    }

}
