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

        var isDirectoryListingRequest = PythonFileProgramParser.IsDirectoryListingRequest(source);
        var isCollectionGroupingRequest = PythonCollectionProgramParser.CollectionGroupingAggregationPattern.IsMatch(source);
        EnglishSentenceSyntax? syntax = null;
        var creationClauseIndex = -1;

        if (!isDirectoryListingRequest && !isCollectionGroupingRequest &&
            !PythonProgramRequestParser.TryGetCreationClause(source, out syntax, out creationClauseIndex))
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
        else if (PythonFileProgramParser.TryGetDirectoryFileListingProgram(source, out var directoryListingSteps))
        {
            formula = formula with { GenerationSteps = directoryListingSteps };
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
        else if (PythonFunctionProgramParser.TryGetFunctionWithLocalVariableProgram(source, out var localVariableSteps))
        {
            formula = formula with { GenerationSteps = localVariableSteps };
        }
        else if (PythonFunctionProgramParser.TryGetFunctionProgram(source, out var functionSteps))
        {
            formula = formula with { GenerationSteps = functionSteps };
        }
        else if (PythonCollectionProgramParser.TryGetProductPriceIterationProgram(source, out var productPriceSteps))
        {
            formula = formula with { GenerationSteps = productPriceSteps };
        }
        else if (PythonCollectionProgramParser.TryGetDictionaryIterationProgram(source, out var dictionarySteps))
        {
            formula = formula with { GenerationSteps = dictionarySteps };
        }
        else if (PythonCollectionProgramParser.TryGetIntegerConversionFunctionProgram(source, out var exceptionSteps))
        {
            formula = formula with { GenerationSteps = exceptionSteps };
        }
        else if (PythonCollectionProgramParser.TryGetCollectionMappingFunctionProgram(source, out var collectionMappingSteps))
        {
            formula = formula with { GenerationSteps = collectionMappingSteps };
        }
        else if (PythonCollectionProgramParser.TryGetCollectionFilterFunctionProgram(source, out var collectionFilterSteps))
        {
            formula = formula with { GenerationSteps = collectionFilterSteps };
        }
        else if (PythonCollectionProgramParser.TryGetSortRecordsByFieldsProgram(source, out var sortRecordsByFieldsSteps))
        {
            formula = formula with { GenerationSteps = sortRecordsByFieldsSteps };
        }
        else if (PythonCollectionProgramParser.TryGetSortRecordsByFieldProgram(source, out var sortRecordsSteps))
        {
            formula = formula with { GenerationSteps = sortRecordsSteps };
        }
        else if (PythonCollectionProgramParser.TryGetJsonCollectionRoundTripProgram(source, out var jsonRoundTripSteps))
        {
            formula = formula with { GenerationSteps = jsonRoundTripSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetLoopFilterProgram(source, out var filterSteps))
        {
            formula = formula with { GenerationSteps = filterSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetLoopProgram(source, out var loopSteps))
        {
            formula = formula with { GenerationSteps = loopSteps };
        }
        else if (PythonProgramRequestParser.TryGetRequestedOutput(syntax, creationClauseIndex, sourceText, out var requestedValue))
        {
            formula = formula with { RequestedValue = requestedValue };
        }
        else
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
