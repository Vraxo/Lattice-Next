using System.Collections.Immutable;
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

        var isCollectionGroupingRequest = PythonCollectionProgramParser.CollectionGroupingAggregationPattern.IsMatch(source);
        if (!isCollectionGroupingRequest &&
            !PythonProgramRequestParser.TryGetCreationClause(source, out _))
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

        if (PythonCollectionProgramParser.TryGetCollectionGroupingAggregationProgram(source, out var groupingSteps))
        {
            formula = formula with { GenerationSteps = groupingSteps };
        }
        else if (PythonFunctionProgramParser.TryGetFunctionCompositionProgram(source, out var compositionSteps))
        {
            formula = formula with { GenerationSteps = compositionSteps };
        }
        else if (PythonFunctionProgramParser.TryGetFunctionMultiConditionalProgram(source, out var multiConditionalFunctionSteps))
        {
            formula = formula with { GenerationSteps = multiConditionalFunctionSteps };
        }
        else if (PythonFunctionProgramParser.TryGetFunctionConditionalProgram(source, out var conditionalFunctionSteps))
        {
            formula = formula with { GenerationSteps = conditionalFunctionSteps };
        }

        else if (PythonCollectionProgramParser.TryGetCollectionFilterFunctionProgram(source, out var collectionFilterSteps))
        {
            formula = formula with { GenerationSteps = collectionFilterSteps };
        }
        else if (PythonCollectionProgramParser.TryGetSortRecordsByFieldProgram(source, out var sortRecordsSteps))
        {
            formula = formula with { GenerationSteps = sortRecordsSteps };
        }
        else if (!PythonProgramRequestParser.HasOutputIntent(source))
        {
            return false;
        }

        utterance = new SemanticUtterance(
            UtteranceKind.TaskRequest,
            formula,
            sourceText.Trim());
        return true;
    }
}
