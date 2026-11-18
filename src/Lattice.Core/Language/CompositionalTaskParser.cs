using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CompositionalTaskParser
{
    private static readonly HashSet<string> CreationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "create", "generate", "make", "build", "define"
    };

    private static readonly HashSet<string> CodeArtifacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "program", "script", "application", "code", "function", "class"
    };

    private static readonly HashSet<string> OutputVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "print", "display", "show", "output", "say"
    };


    private static readonly Regex AssignmentValuePattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?:\d+(?:\.\d+)?)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex VariableExpressionProgramPattern = new(
        @"\bsets?\s+(?<variable>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<value>\d+(?:\.\d+)?)\s+and\s+(?:prints?|displays?|outputs?)\s+(?<expression>.+?)\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TwoNumberAdditionPattern = new(
        @"\bstores?\s+(?:two|2)\s+numbers?\s+in\s+variables?,?\s+adds?\s+them,?\s+and\s+(?:prints?|displays?|outputs?)\s+(?:the\s+)?result\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex IntegerSignClassificationPattern = new(
        @"\b(?:classifies?\s+(?:an|the)\s+integer\s+as|reports?\s+whether\s+(?:an|the)\s+integer\s+is)\s+positive,?\s+negative,?\s+or\s+zero\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);


    private static readonly Regex QuotedOutputPattern = new(
        @"\b(?:prints?|displays?|shows?|outputs?|says?)\s+(['""])(?<value>.*?)\1",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        var isDirectoryListingRequest =
            PythonFileProgramParser.DirectoryFileListingPattern.IsMatch(source) &&
            Regex.IsMatch(source, @"\bPython\s+program\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var isCollectionGroupingRequest = PythonCollectionProgramParser.CollectionGroupingAggregationPattern.IsMatch(source);
        EnglishSentenceSyntax? syntax = null;
        var creationClauseIndex = -1;

        if (!isDirectoryListingRequest && !isCollectionGroupingRequest)
        {
            if (!EnglishSyntaxParser.TryParse(source, out syntax))
            {
                return false;
            }

            for (var index = 0; index < syntax!.Clauses.Length; index++)
            {
                if (CreationVerbs.Contains(syntax.Clauses[index].Verb))
                {
                    creationClauseIndex = index;
                    break;
                }
            }

            if (creationClauseIndex < 0 ||
                syntax.Clauses[creationClauseIndex].Object is not { } codeArtifact ||
                !ContainsCodeArtifact(codeArtifact) ||
                !ContainsPythonLanguage(syntax.Clauses[creationClauseIndex]))
            {
                return false;
            }
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
        else if (PythonClassProgramParser.TryGetBankAccountClassProgram(source, out var bankAccountSteps))
        {
            formula = formula with { GenerationSteps = bankAccountSteps };
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
        else if (PythonFunctionProgramParser.TryGetGreetingFunctionProgram(source, out var greetingSteps))
        {
            formula = formula with { GenerationSteps = greetingSteps };
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
        else if (PythonFileProgramParser.TryGetNonEmptyFileLineCountProgram(source, out var fileLineCountSteps))
        {
            formula = formula with { GenerationSteps = fileLineCountSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetLoopFilterProgram(source, out var filterSteps))
        {
            formula = formula with { GenerationSteps = filterSteps };
        }
        else if (TryGetTwoNumberAdditionProgram(source, out var additionSteps))
        {
            formula = formula with { GenerationSteps = additionSteps };
        }
        else if (TryGetIntegerSignClassificationProgram(source, out var signClassificationSteps))
        {
            formula = formula with { GenerationSteps = signClassificationSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetGenericEvenListFilterProgram(source, out var evenFilterSteps))
        {
            formula = formula with { GenerationSteps = evenFilterSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetBuiltInSumAvoidanceProgram(source, out var manualTotalSteps))
        {
            formula = formula with { GenerationSteps = manualTotalSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetLoopAccumulatorProgram(source, out var accumulatorSteps))
        {
            formula = formula with { GenerationSteps = accumulatorSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetLoopProgram(source, out var loopSteps))
        {
            formula = formula with { GenerationSteps = loopSteps };
        }
        else if (PythonControlFlowProgramParser.TryGetConditionalProgram(source, out var conditionalSteps))
        {
            formula = formula with { GenerationSteps = conditionalSteps };
        }
        else if (TryGetVariableProgram(source, out var generationSteps))
        {
            formula = formula with { GenerationSteps = generationSteps };
        }
        else if (TryGetRequestedOutput(syntax, creationClauseIndex, sourceText, out var requestedValue))
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

    private static bool ContainsCodeArtifact(EnglishNounPhrase phrase) =>
        CodeArtifacts.Contains(phrase.Head) ||
        phrase.Modifiers.Any(CodeArtifacts.Contains);

    private static bool ContainsPythonLanguage(EnglishClause clause) =>
        ContainsWord(clause.Object, "python") ||
        clause.PrepositionalPhrases.Any(phrase => ContainsWord(phrase.Object, "python"));

    private static bool ContainsWord(EnglishNounPhrase? phrase, string word) =>
        phrase is not null &&
        (phrase.Head.Equals(word, StringComparison.OrdinalIgnoreCase) ||
         phrase.Modifiers.Any(modifier => modifier.Equals(word, StringComparison.OrdinalIgnoreCase)));


    private static bool TryGetTwoNumberAdditionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!TwoNumberAdditionPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "first_number = 10"),
            new TaskRequestStep("assign-variable", "assignment", "second_number = 20"),
            new TaskRequestStep("assign-variable", "assignment", "result = first_number + second_number"),
            new TaskRequestStep("write-standard-output", "expression", "result"));
        return true;
    }

    private static bool TryGetIntegerSignClassificationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!IntegerSignClassificationPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "number = -5"),
            new TaskRequestStep("begin-conditional", "condition", "number > 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Positive"),
            new TaskRequestStep("begin-conditional", "condition", "number < 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Negative"),
            new TaskRequestStep("begin-conditional", "condition", "number == 0"),
            new TaskRequestStep("write-standard-output-indented", "text", "Zero"));
        return true;
    }


    private static bool TryGetVariableProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = VariableExpressionProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var variable = match.Groups["variable"].Value;
        var assignedValue = match.Groups["value"].Value;
        var expression = match.Groups["expression"].Value.Trim().TrimEnd('.', '?', '!');

        if (!AssignmentValuePattern.IsMatch($"{variable} = {assignedValue}") ||
            !PythonExpressionParser.IsArithmeticExpression(expression))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep(
                "assign-variable",
                "assignment",
                $"{variable} = {assignedValue}"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                expression));
        return true;
    }

    private static bool TryGetRequestedOutput(
        EnglishSentenceSyntax syntax,
        int creationClauseIndex,
        string sourceText,
        out string value)
    {
        var outputClause = syntax.Clauses
            .Skip(creationClauseIndex + 1)
            .FirstOrDefault(clause => OutputVerbs.Contains(clause.Verb));

        if (outputClause?.Object is not null &&
            (ContainsHelloWorld(outputClause.Object) ||
             NormalizeOutput(outputClause.Object.Text).Equals("Hello, world!", StringComparison.Ordinal) ||
             QuotedOutputPattern.IsMatch(sourceText)))
        {
            value = NormalizeOutput(outputClause.Object.Text);
            return value.Length > 0;
        }

        var artifact = syntax.Clauses[creationClauseIndex].Object;
        if (artifact is not null && ContainsHelloWorld(artifact))
        {
            value = "Hello, world!";
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool ContainsHelloWorld(EnglishNounPhrase phrase)
    {
        for (var index = 0; index < phrase.Modifiers.Length - 1; index++)
        {
            if (phrase.Modifiers[index].Equals("hello", StringComparison.OrdinalIgnoreCase) &&
                phrase.Modifiers[index + 1].Equals("world", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeOutput(string value)
    {
        var normalized = value.Trim().TrimEnd('.', '?');
        if (normalized.Equals("hello world", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("hello, world", StringComparison.OrdinalIgnoreCase))
        {
            return "Hello, world!";
        }

        return normalized;
    }
}
