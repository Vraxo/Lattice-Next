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

    private static readonly Regex LoopRangeOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>numbers?|values?|items?|counts?|i)\s+from\s+(?<start>\d+)\s+(?:to|through)\s+(?<end>\d+)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopCollectionOutputPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopFilterPattern = new(
        @"\b(?:print|display|output)s?\s+(?:each\s+)?(?<item>numbers?|values?|items?)\s+(?:in|from)\s+(?:the\s+)?(?:list\s+)?(?<items>\[[^\]]*\])\s+only\s+if\s+(?:they\s+are|it\s+is)\s+(?<parity>even|odd)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DictionaryIterationPattern = new(
        @"\bstores?\s+(?<items>\{[^}]*\})\s+(?:in|as)\s+(?:a\s+)?dictionary\s+(?:named\s+)?(?<dictionary>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s+then\s+(?:prints?|displays?|outputs?)\s+each\s+(?<key>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+(?:its\s+)?(?<value>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ProductPriceIterationPattern = new(
        @"\b(?:given|with)\s+(?:a\s+)?dictionary\s+of\s+product\s+names\s+and\s+prices\s*,?\s*(?:prints?|displays?|outputs?)\s+each\s+product\s+with\s+its\s+price\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BankAccountClassPattern = new(
        @"\bclass\s+(?:representing|for)\s+(?:a\s+)?bank\s+account\b.*\bbalance\b.*\bdeposit(?:ion)?\s+method\b.*\bwithdraw(?:al)?\s+method\b.*\b(?:refuses?|rejects?)\s+withdrawals?\s+(?:that\s+are\s+)?(?:larger|greater)\s+than\s+(?:the\s+)?balance\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex GreetingFunctionPattern = new(
        @"\b(?:defines?|creates?)\s+(?:a\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|takes?)\s+(?:a\s+)?person'?s\s+name\s+and\s+returns?\s+(?:a\s+)?greeting\b.*?\bcall\s+(?:it|the\s+function)\s+with\s+two\s+different\s+names\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex IntegerConversionFunctionPattern = new(
        @"\bfunction\s+(?:named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?:that\s+)?(?:accepts?\s+(?<parameter>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+)?converts?\s+(?:the\s+)?(?<input>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+an?\s+integer\s+and\s+returns?\s+(?:a\s+)?(?:clear\s+)?(?:result|message)\s+(?:when|if)\s+(?:the\s+)?(?:text|input)\s+is\s+not\s+a\s+valid\s+integer",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CollectionMappingFunctionPattern = new(
        @"\b(?:given|from)\s+(?:a|the)\s+list\s+of\s+(?<kind>names?|words?|numbers?|values?|items?)\s*,?\s*(?:creates?|builds?|makes?)\s+(?:a\s+)?dictionary(?:\s+named\s+(?<dictionary>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+maps?\s+each\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<mapping>.+?)\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CollectionFilterFunctionPattern = new(
        @"\bfunction\s+(?:named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?:that\s+)?(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+numbers?(?:\s+(?:named\s+)?(?<parameter>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+returns?\s+(?:a\s+)?new\s+list\s+containing\s+only\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)(?:\s+values?)?\s+(?<comparison>greater\s+than\s+or\s+equal\s+to|at\s+least|less\s+than\s+or\s+equal\s+to|at\s+most|not\s+equal\s+to|greater\s+than|more\s+than|less\s+than|below|equal\s+to|equals)\s+(?<threshold>(?:the\s+)?average(?:\s+of\s+(?:the\s+)?(?<averageParameter>[A-Za-z_][A-Za-z0-9_]*))?|\d+(?:\.\d+)?(?:\s*[+*/%\-]\s*\d+(?:\.\d+)?)*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SortRecordsByFieldsPattern = new(
        @"\b(?:creates?|defines?|writes?)\s+(?:a\s+)?(?:reusable\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+records(?:\s+(?:named\s+)?(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+(?:a\s+)?list\s+of\s+field\s+names(?:\s+(?:named\s+)?(?<fields>[A-Za-z_][A-Za-z0-9_]*))?\s*,?\s+then\s+returns?\s+(?:the\s+)?records?\s+sorted\s+(?:case[- ]insensitively\s+)?by\s+(?:those|the)\s+fields(?:\s+case[- ]insensitively)?(?:\s+in\s+descending\s+order)?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SortRecordsByFieldPattern = new(
        @"\b(?:creates?|defines?|writes?)\s+(?:a\s+)?(?:reusable\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|receives?)\s+(?:a\s+)?list\s+of\s+records(?:\s+(?:named\s+)?(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+and\s+(?:a\s+)?field\s+name(?:\s+(?:named\s+)?(?<field>[A-Za-z_][A-Za-z0-9_]*))?\s*,?\s+then\s+returns?\s+(?:the\s+)?records?\s+sorted\s+(?:case[- ]insensitively\s+)?by\s+(?:that|the)\s+field(?:\s+case[- ]insensitively)?(?:\s+in\s+(?:descending|reverse)\s+order)?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex JsonCollectionRoundTripPattern = new(
        @"\b(?:saves?|writes?)\s+(?:a|the)\s+list\s+of\s+dictionaries(?:\s+(?:named|called)\s+(?<records>[A-Za-z_][A-Za-z0-9_]*))?\s+to\s+(?:a|the)\s+JSON\s+file(?:\s+(?:named|called)\s+(?<path>[A-Za-z0-9_.-]+\.json))?\s+and\s+then\s+loads?\s+it\s+back\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DirectoryFileListingPattern = new(
        @"\b(?:accepts?|takes?)\s+(?:a|the)\s+directory\s+path\s+and\s+reports?\s+(?:the\s+)?names?\s+and\s+sizes?\s+of\s+(?:all\s+)?files?\s+(?:directly\s+)?inside\s+it,?\s+handling\s+(?:a\s+)?missing\s+directory\s+cleanly\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CollectionGroupingAggregationPattern = new(
        @"\b(?:groups?|organizes?)\s+(?:a|the)\s+list\s+of\s+(?<record>[A-Za-z_][A-Za-z0-9_]*)\s+records?\s+by\s+(?<group>[A-Za-z_][A-Za-z0-9_]*)\s+and\s+(?:calculates?|computes?|sums?)\s+(?:each\s+(?<owner>[A-Za-z_][A-Za-z0-9_]*)'?s\s+)?(?:total\s+)?(?<measure>spending|amount|cost|price|value)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex NonEmptyFileLineCountPattern = new(
        @"\breads?\s+(?:a|the)\s+text\s+file\s+and\s+counts?\s+how\s+many\s+non[- ]empty\s+lines?\s+it\s+contains?\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BuiltInSumAvoidancePattern = new(
        @"\bcalculates?\s+(?:(?:their|the)\s+(?:total|sum)|(?:the\s+)?(?:total|sum)\s+of\s+(?:a|the)\s+list\s+of\s+numbers?)\s+without\s+using\s+(?:the\s+)?built[- ]in\s+sum\s+function\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopAccumulatorPattern = new(
        @"\b(?:starts?|initializes?)\s+(?<total>[A-Za-z_][A-Za-z0-9_]*)\s+(?:at|to)\s+0\s+and\s+adds?\s+each\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+in\s+(?<items>\[[^\]]*\])\s+to\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s+then\s+(?:prints?|displays?|outputs?)\s+(?<output>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionDefinitionPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<expression>.+?)(?=,\s*(?:and\s+)?(?:a\s+)?function\s+named\b|,\s*then\s+(?:print|display|output)\b)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionCompositionCallPattern = new(
        @"\bthen\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<expression>.+?)\s*,?\s+then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionMultiConditionalProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<branchValue>.+?)\s+when\s+(?<branchCondition>.+?)(?:,\s*(?:and\s+)?returns\s+(?<branchValue>.+?)\s+when\s+(?<branchCondition>.+?))*\s*,?\s+and\s+otherwise\s+returns\s+(?<falseValue>.+?),?\s+then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionConditionalProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<trueValue>.+?)\s+when\s+(?<condition>.+?)\s+and\s+otherwise\s+returns\s+(?<falseValue>.+?),?\s+then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionLocalVariableProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+sets?\s+(?<local>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<assignment>.+?)(?:\s+and\s+sets?\s+(?<local>[A-Za-z_][A-Za-z0-9_]*)\s+to\s+(?<assignment>.+?))*\s+and\s+returns\s+(?<expression>.+?),\s*then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ConditionalOutputPattern = new(
        @"\b(?:print|display|show|output)s?\s+(?<quote>['""])(?<text>[^'""]+)\k<quote>\s+if\s+(?<left>\d+(?:\.\d+)?)\s+(?<operator>is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+equal\s+to|equals?)\s+(?<right>\d+(?:\.\d+)?)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

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

    private static readonly Regex GenericEvenListFilterPattern = new(
        @"\b(?:prints?|displays?|outputs?)\s+only\s+the\s+even\s+numbers?\s+from\s+(?:a|the)\s+list\b",
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
            DirectoryFileListingPattern.IsMatch(source) &&
            Regex.IsMatch(source, @"\bPython\s+program\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var isCollectionGroupingRequest = CollectionGroupingAggregationPattern.IsMatch(source);
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

        if (TryGetCollectionGroupingAggregationProgram(source, out var groupingSteps))
        {
            formula = formula with { GenerationSteps = groupingSteps };
        }
        else if (TryGetDirectoryFileListingProgram(source, out var directoryListingSteps))
        {
            formula = formula with { GenerationSteps = directoryListingSteps };
        }
        else if (TryGetBankAccountClassProgram(source, out var bankAccountSteps))
        {
            formula = formula with { GenerationSteps = bankAccountSteps };
        }
        else if (TryGetFunctionCompositionProgram(source, out var compositionSteps))
        {
            formula = formula with { GenerationSteps = compositionSteps };
        }
        else if (TryGetFunctionMultiConditionalProgram(source, out var multiConditionalFunctionSteps))
        {
            formula = formula with { GenerationSteps = multiConditionalFunctionSteps };
        }
        else if (TryGetFunctionConditionalProgram(source, out var conditionalFunctionSteps))
        {
            formula = formula with { GenerationSteps = conditionalFunctionSteps };
        }
        else if (TryGetFunctionWithLocalVariableProgram(source, out var localVariableSteps))
        {
            formula = formula with { GenerationSteps = localVariableSteps };
        }
        else if (TryGetGreetingFunctionProgram(source, out var greetingSteps))
        {
            formula = formula with { GenerationSteps = greetingSteps };
        }
        else if (TryGetFunctionProgram(source, out var functionSteps))
        {
            formula = formula with { GenerationSteps = functionSteps };
        }
        else if (TryGetProductPriceIterationProgram(source, out var productPriceSteps))
        {
            formula = formula with { GenerationSteps = productPriceSteps };
        }
        else if (TryGetDictionaryIterationProgram(source, out var dictionarySteps))
        {
            formula = formula with { GenerationSteps = dictionarySteps };
        }
        else if (TryGetIntegerConversionFunctionProgram(source, out var exceptionSteps))
        {
            formula = formula with { GenerationSteps = exceptionSteps };
        }
        else if (TryGetCollectionMappingFunctionProgram(source, out var collectionMappingSteps))
        {
            formula = formula with { GenerationSteps = collectionMappingSteps };
        }
        else if (TryGetCollectionFilterFunctionProgram(source, out var collectionFilterSteps))
        {
            formula = formula with { GenerationSteps = collectionFilterSteps };
        }
        else if (TryGetSortRecordsByFieldsProgram(source, out var sortRecordsByFieldsSteps))
        {
            formula = formula with { GenerationSteps = sortRecordsByFieldsSteps };
        }
        else if (TryGetSortRecordsByFieldProgram(source, out var sortRecordsSteps))
        {
            formula = formula with { GenerationSteps = sortRecordsSteps };
        }
        else if (TryGetJsonCollectionRoundTripProgram(source, out var jsonRoundTripSteps))
        {
            formula = formula with { GenerationSteps = jsonRoundTripSteps };
        }
        else if (TryGetNonEmptyFileLineCountProgram(source, out var fileLineCountSteps))
        {
            formula = formula with { GenerationSteps = fileLineCountSteps };
        }
        else if (TryGetLoopFilterProgram(source, out var filterSteps))
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
        else if (TryGetGenericEvenListFilterProgram(source, out var evenFilterSteps))
        {
            formula = formula with { GenerationSteps = evenFilterSteps };
        }
        else if (TryGetBuiltInSumAvoidanceProgram(source, out var manualTotalSteps))
        {
            formula = formula with { GenerationSteps = manualTotalSteps };
        }
        else if (TryGetLoopAccumulatorProgram(source, out var accumulatorSteps))
        {
            formula = formula with { GenerationSteps = accumulatorSteps };
        }
        else if (TryGetLoopProgram(source, out var loopSteps))
        {
            formula = formula with { GenerationSteps = loopSteps };
        }
        else if (TryGetConditionalProgram(source, out var conditionalSteps))
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


    private static bool TryGetFunctionCompositionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var definitions = FunctionDefinitionPattern.Matches(source);
        var callMatch = FunctionCompositionCallPattern.Match(source);
        if (definitions.Count < 2 || !callMatch.Success)
        {
            return false;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var generatedSteps = ImmutableArray.CreateBuilder<TaskRequestStep>();
        foreach (Match definition in definitions)
        {
            var name = definition.Groups["name"].Value;
            var parameters = definition.Groups["parameters"].Value
                .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var expression = definition.Groups["expression"].Value.Trim();

            var composesEarlierFunction = names.Any(previousName =>
                Regex.IsMatch(
                    expression,
                    $@"\b{Regex.Escape(previousName)}\s*\(",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase));

            if (!PythonExpressionParser.IsArithmeticExpression(expression) ||
                (names.Count > 0 && !composesEarlierFunction) ||
                !names.Add(name))
            {
                return false;
            }

            generatedSteps.Add(new TaskRequestStep(
                "begin-function",
                "function-header",
                $"{name}({string.Join(", ", parameters)})"));
            generatedSteps.Add(new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                expression));
        }

        var finalName = definitions[^1].Groups["name"].Value;
        var finalParameters = definitions[^1].Groups["parameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var compactCall = Regex.Replace(callMatch.Groups["call"].Value, @"\s+", string.Empty);
        var numericArguments = string.Join(",", Enumerable.Repeat(@"[+-]?\d+(?:\.\d+)?", finalParameters.Length));
        if (!Regex.IsMatch(
            compactCall,
            $@"^{Regex.Escape(finalName)}\({numericArguments}\)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        var callName = compactCall[..compactCall.IndexOf('(')];
        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        generatedSteps.Add(new TaskRequestStep(
            "write-standard-output",
            "expression",
            $"{callName}({string.Join(", ", callArguments)})"));
        steps = generatedSteps.ToImmutable();
        return true;
    }

    private static bool TryGetFunctionMultiConditionalProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = FunctionMultiConditionalProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var parameters = match.Groups["parameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var branchValues = match.Groups["branchValue"].Captures
            .Select(capture => capture.Value)
            .ToArray();
        var branchConditions = match.Groups["branchCondition"].Captures
            .Select(capture => capture.Value)
            .ToArray();
        var compactCall = Regex.Replace(match.Groups["call"].Value, @"\s+", string.Empty);
        var functionArguments = string.Join(",", Enumerable.Repeat(@"(?:[+-]?\d+(?:\.\d+)?|'[^']*')", parameters.Length));

        if (branchValues.Length < 2 ||
            branchValues.Length != branchConditions.Length ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(name)}\({functionArguments}\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) ||
            !PythonExpressionParser.TryGetFunctionReturnExpression(match.Groups["falseValue"].Value, parameters, out var falseValue))
        {
            return false;
        }

        var generatedSteps = ImmutableArray.CreateBuilder<TaskRequestStep>();
        generatedSteps.Add(new TaskRequestStep(
            "begin-function",
            "function-header",
            $"{name}({string.Join(", ", parameters)})"));

        for (var index = 0; index < branchValues.Length; index++)
        {
            if (!PythonExpressionParser.TryParseFunctionCondition(branchConditions[index], parameters, out var pythonCondition) ||
                !PythonExpressionParser.TryGetFunctionReturnExpression(branchValues[index], parameters, out var returnValue))
            {
                return false;
            }

            generatedSteps.Add(new TaskRequestStep(
                index == 0 ? "begin-conditional-indented" : "begin-elif-indented",
                "condition",
                pythonCondition));
            generatedSteps.Add(new TaskRequestStep(
                "return-value-double-indented-expression",
                "expression",
                returnValue));
        }

        generatedSteps.Add(new TaskRequestStep(
            "return-value-indented-expression",
            "expression",
            falseValue));

        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        generatedSteps.Add(new TaskRequestStep(
            "write-standard-output",
            "expression",
            $"{name}({string.Join(", ", callArguments)})"));

        steps = generatedSteps.ToImmutable();
        return true;
    }

    private static bool TryGetFunctionConditionalProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = FunctionConditionalProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var parameters = match.Groups["parameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var condition = match.Groups["condition"].Value;
        var compactCall = Regex.Replace(match.Groups["call"].Value, @"\s+", string.Empty);
        var functionArguments = string.Join(",", Enumerable.Repeat(@"(?:[+-]?\d+(?:\.\d+)?|'[^']*')", parameters.Length));

        if (!PythonExpressionParser.TryParseFunctionCondition(condition, parameters, out var pythonCondition) ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(name)}\({functionArguments}\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (!PythonExpressionParser.TryGetFunctionReturnExpression(match.Groups["trueValue"].Value, parameters, out var trueValue) ||
            !PythonExpressionParser.TryGetFunctionReturnExpression(match.Groups["falseValue"].Value, parameters, out var falseValue))
        {
            return false;
        }

        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({string.Join(", ", parameters)})"),
            new TaskRequestStep("begin-conditional-indented", "condition", pythonCondition),
            new TaskRequestStep("return-value-double-indented-expression", "expression", trueValue),
            new TaskRequestStep("return-value-indented-expression", "expression", falseValue),
            new TaskRequestStep("write-standard-output", "expression", $"{name}({string.Join(", ", callArguments)})"));
        return true;
    }

    private static bool TryGetFunctionWithLocalVariableProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = FunctionLocalVariableProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var parameters = match.Groups["parameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var locals = match.Groups["local"].Captures
            .Select(capture => capture.Value)
            .ToArray();
        var assignments = match.Groups["assignment"].Captures
            .Select(capture => capture.Value.Trim())
            .ToArray();
        var expression = match.Groups["expression"].Value.Trim();
        var compactCall = Regex.Replace(match.Groups["call"].Value, @"\s+", string.Empty);
        var numericArguments = string.Join(",", Enumerable.Repeat(@"[+-]?\d+(?:\.\d+)?", parameters.Length));

        if (locals.Length == 0 ||
            locals.Length != assignments.Length ||
            locals.Distinct(StringComparer.OrdinalIgnoreCase).Count() != locals.Length ||
            locals.Any(local => parameters.Contains(local, StringComparer.OrdinalIgnoreCase)) ||
            assignments.Any(assignment => !PythonExpressionParser.IsArithmeticExpression(assignment)) ||
            !PythonExpressionParser.IsArithmeticExpression(expression) ||
            !locals.Any(local => Regex.IsMatch(
                expression,
                $@"\b{Regex.Escape(local)}\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)) ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(name)}\({numericArguments}\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        var availableNames = new HashSet<string>(parameters, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < assignments.Length; index++)
        {
            var identifiers = Regex.Matches(assignments[index], @"\b[A-Za-z_][A-Za-z0-9_]*\b")
                .Select(capture => capture.Value);
            if (identifiers.Any(identifier => !availableNames.Contains(identifier)))
            {
                return false;
            }

            availableNames.Add(locals[index]);
        }

        var returnIdentifiers = Regex.Matches(expression, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
            .Select(capture => capture.Value);
        if (returnIdentifiers.Any(identifier =>
                !availableNames.Contains(identifier) &&
                !Regex.IsMatch(expression, $@"\b{Regex.Escape(identifier)}\s*\(",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)))
        {
            return false;
        }

        var callName = compactCall[..compactCall.IndexOf('(')];
        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        var generatedSteps = ImmutableArray.CreateBuilder<TaskRequestStep>();
        generatedSteps.Add(new TaskRequestStep(
            "begin-function",
            "function-header",
            $"{name}({string.Join(", ", parameters)})"));
        for (var index = 0; index < assignments.Length; index++)
        {
            generatedSteps.Add(new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{locals[index]} = {assignments[index]}"));
        }

        generatedSteps.Add(new TaskRequestStep(
            "return-value-indented-expression",
            "expression",
            expression));
        generatedSteps.Add(new TaskRequestStep(
            "write-standard-output",
            "expression",
            $"{callName}({string.Join(", ", callArguments)})"));
        steps = generatedSteps.ToImmutable();
        return true;
    }

    private static bool TryGetBankAccountClassProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!BankAccountClassPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-class", "class-name", "BankAccount"),
            new TaskRequestStep("begin-class-method", "method-header", "__init__(self, balance)"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance = balance"),
            new TaskRequestStep("begin-class-method", "method-header", "deposit(self, amount)"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance += amount"),
            new TaskRequestStep("begin-class-method", "method-header", "withdraw(self, amount)"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "amount > self.balance"),
            new TaskRequestStep("return-value-triple-indented-expression", "expression", "False"),
            new TaskRequestStep("assign-variable-double-indented", "assignment", "self.balance -= amount"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", "True"));
        return true;
    }

    private static bool TryGetGreetingFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = GreetingFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Success ? match.Groups["name"].Value : "greet";
        const string parameter = "name";
        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("return-value-indented-expression", "expression", "f\"Hello, {name}!\""),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"Ada\")"),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"Grace\")"));
        return true;
    }

    private static bool TryGetFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = FunctionProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var name = match.Groups["name"].Value;
        var parameters = match.Groups["parameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var expression = match.Groups["expression"].Value.Trim();
        var compactCall = Regex.Replace(match.Groups["call"].Value, @"\s+", string.Empty);
        var numericArguments = string.Join(",", Enumerable.Repeat(@"[+-]?\d+(?:\.\d+)?", parameters.Length));

        if (!PythonExpressionParser.IsArithmeticExpression(expression) ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(name)}\({numericArguments}\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        var callName = compactCall[..compactCall.IndexOf('(')];
        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        var call = $"{callName}({string.Join(", ", callArguments)})";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({string.Join(", ", parameters)})"),
            new TaskRequestStep("return-value-indented-expression", "expression", expression),
            new TaskRequestStep("write-standard-output", "expression", call));
        return true;
    }

    private static bool TryGetCollectionMappingFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionMappingFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var kind = match.Groups["kind"].Value;
        var item = match.Groups["item"].Value;
        var parameter = kind.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? kind
            : kind + "s";
        var expressionText = match.Groups["mapping"].Value.Trim().TrimEnd('.', '?', '!');
        var expression = expressionText.ToLowerInvariant() switch
        {
            "the number of characters in it" or "the length of it" or "its length" =>
                $"len({item})",
            "the square of it" or "its square" =>
                $"{item} * {item}",
            _ => expressionText
        };

        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            !PythonExpressionParser.IsArithmeticExpression(expression) ||
            Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*")
                .Any(identifier => !identifier.Value.Equals(item, StringComparison.OrdinalIgnoreCase) &&
                    !identifier.Value.Equals("len", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var function = "map_values";
        var sample = kind.StartsWith("name", StringComparison.OrdinalIgnoreCase) ||
            kind.StartsWith("word", StringComparison.OrdinalIgnoreCase)
            ? @"[""Ada"", ""Grace"", ""Linus""]"
            : "[1, 2, 3]";

        var dictionary = match.Groups["dictionary"].Success
            ? match.Groups["dictionary"].Value
            : "mapped_values";
        var dictionaryExpression = $"{{{item}: {expression} for {item} in {parameter}}}";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{function}({parameter})"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{dictionary} = {dictionaryExpression}"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                dictionary),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{function}({sample})"));
        return true;
    }

    private static bool TryGetCollectionFilterFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionFilterFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var threshold = match.Groups["threshold"].Value;
        var isAverage = threshold.Contains("average", StringComparison.OrdinalIgnoreCase);
        var name = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : isAverage ? "above_average" : "filter_values";
        var parameter = match.Groups["parameter"].Success ? match.Groups["parameter"].Value : "values";
        var item = match.Groups["item"].Value;
        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) &&
            item.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            item = item[..^1];
        }

        var averageParameter = match.Groups["averageParameter"].Success
            ? match.Groups["averageParameter"].Value
            : parameter;

        if (item.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            (isAverage && !averageParameter.Equals(parameter, StringComparison.OrdinalIgnoreCase)) ||
            name.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(item, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var comparisonText = Regex.Replace(
            match.Groups["comparison"].Value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim().ToLowerInvariant();
        var comparison = comparisonText switch
        {
            "greater than" or "more than" => ">",
            "greater than or equal to" or "at least" => ">=",
            "less than" or "below" => "<",
            "less than or equal to" or "at most" => "<=",
            "equal to" or "equals" => "==",
            "not equal to" => "!=",
            _ => string.Empty
        };
        var thresholdExpression = isAverage
            ? $"sum({parameter}) / len({parameter})"
            : threshold;
        if (comparison.Length == 0 || (!isAverage && !PythonExpressionParser.IsArithmeticExpression(thresholdExpression)))
        {
            return false;
        }
        var expression = $"[{item} for {item} in {parameter} if {item} {comparison} {thresholdExpression}]";
        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("return-value-indented-expression", "expression", expression),
            new TaskRequestStep("write-standard-output", "expression", $"{name}([1, 2, 3, 4, 5])"));
        return true;
    }

    private static bool TryGetIntegerConversionFunctionProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = IntegerConversionFunctionPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var parameter = match.Groups["parameter"].Success
            ? match.Groups["parameter"].Value
            : match.Groups["input"].Value;
        var name = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "convert_to_integer";

        if (name.Equals(parameter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{name}({parameter})"),
            new TaskRequestStep("begin-try-indented", "statement", "try"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", $"int({parameter})"),
            new TaskRequestStep("begin-except-indented", "exception-type", "ValueError"),
            new TaskRequestStep("return-value-double-indented-expression", "expression", "\"Invalid integer\""),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"42\")"),
            new TaskRequestStep("write-standard-output", "expression", $"{name}(\"not-a-number\")"));
        return true;
    }

    private static bool TryGetProductPriceIterationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!ProductPriceIterationPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "products = {\"Apple\": 1.5, \"Banana\": 0.75, \"Orange\": 1.25}"),
            new TaskRequestStep("begin-loop", "loop-header", "product, price in products.items()"),
            new TaskRequestStep("write-standard-output-indented-expression", "expression", "product, price"));
        return true;
    }

    private static bool TryGetDictionaryIterationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = DictionaryIterationPattern.Match(source);
        if (!match.Success ||
            !TryNormalizeSimpleDictionaryLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        var dictionary = match.Groups["dictionary"].Value;
        var key = match.Groups["key"].Value;
        var value = match.Groups["value"].Value;
        if (dictionary.Equals(key, StringComparison.OrdinalIgnoreCase) ||
            dictionary.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", $"{dictionary} = {items}"),
            new TaskRequestStep(
                "begin-loop",
                "loop-header",
                $"{key}, {value} in {dictionary}.items()"),
            new TaskRequestStep(
                "write-standard-output-indented-expression",
                "expression",
                $"{key}, {value}"));
        return true;
    }

    private static bool TryGetSortRecordsByFieldsProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = SortRecordsByFieldsPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "sort_records";
        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var fieldsName = match.Groups["fields"].Success
            ? match.Groups["fields"].Value
            : "fields";
        var caseInsensitive = source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase)
            || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase);
        var descending = source.Contains("descending order", StringComparison.OrdinalIgnoreCase);
        var sampleRecords = """[{"department": "Engineering", "name": "Ada"}, {"department": "Design", "name": "Grace"}, {"department": "Engineering", "name": "Linus"}]""";
        var sortKey = caseInsensitive
            ? $"tuple(record[field].casefold() for field in {fieldsName})"
            : $"tuple(record[field] for field in {fieldsName})";

        steps = ImmutableArray.Create(
            new TaskRequestStep(
                "begin-function",
                "function-header",
                $"{functionName}({recordsName}, {fieldsName})"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                $"sorted({recordsName}, key=lambda record: {sortKey}{(descending ? ", reverse=True" : string.Empty)})"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{functionName}({sampleRecords}, [\"department\", \"name\"])"));
        return true;
    }

    private static bool TryGetSortRecordsByFieldProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = SortRecordsByFieldPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["name"].Success
            ? match.Groups["name"].Value
            : "sort_records";
        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var fieldName = match.Groups["field"].Success
            ? match.Groups["field"].Value
            : "field";
        var caseInsensitive = source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase)
            || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase);
        var sampleRecords = caseInsensitive
            ? "[{'name': 'ada', 'age': 36}, {'name': 'BOB', 'age': 42}, {'name': 'Grace', 'age': 85}]"
            : "[{\"name\": \"Ada\", \"age\": 36}, {\"name\": \"Grace\", \"age\": 85}, {\"name\": \"Linus\", \"age\": 55}]";
        var sampleField = caseInsensitive ? "'name'" : "\"age\"";

        steps = ImmutableArray.Create(
            new TaskRequestStep(
                "begin-function",
                "function-header",
                $"{functionName}({recordsName}, {fieldName})"),
            new TaskRequestStep(
                "return-value-indented-expression",
                "expression",
                $"sorted({recordsName}, key=lambda record: record[{fieldName}]{(source.Contains("case-insensitively", StringComparison.OrdinalIgnoreCase) || source.Contains("case insensitively", StringComparison.OrdinalIgnoreCase) ? ".casefold()" : string.Empty)}{(source.Contains("descending order", StringComparison.OrdinalIgnoreCase) || source.Contains("reverse order", StringComparison.OrdinalIgnoreCase) ? ", reverse=True" : string.Empty)})"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                $"{functionName}({sampleRecords}, {sampleField})"));
        return true;
    }

    private static bool TryGetJsonCollectionRoundTripProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = JsonCollectionRoundTripPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var recordsName = match.Groups["records"].Success
            ? match.Groups["records"].Value
            : "records";
        var filePath = match.Groups["path"].Success
            ? match.Groups["path"].Value
            : "records.json";
        var loadedName = $"loaded_{recordsName}";

        steps = ImmutableArray.Create(
            new TaskRequestStep("import-module", "module-name", "json"),
            new TaskRequestStep(
                "assign-variable",
                "assignment",
                $"{recordsName} = [{{\"name\": \"Ada\", \"age\": 36}}, {{\"name\": \"Grace\", \"age\": 85}}]"),
            new TaskRequestStep(
                "begin-with",
                "expression",
                $"open(\"{filePath}\", \"w\", encoding=\"utf-8\") as output_file"),
            new TaskRequestStep(
                "write-expression-indented",
                "expression",
                $"{recordsName}, output_file, indent=2"),
            new TaskRequestStep(
                "begin-with",
                "expression",
                $"open(\"{filePath}\", \"r\", encoding=\"utf-8\") as input_file"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{loadedName} = json.load(input_file)"),
            new TaskRequestStep(
                "write-standard-output",
                "expression",
                loadedName));
        return true;
    }


    private static bool TryGetCollectionGroupingAggregationProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = CollectionGroupingAggregationPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var record = match.Groups["record"].Value;
        var group = match.Groups["group"].Value;
        var owner = match.Groups["owner"].Success ? match.Groups["owner"].Value : group;
        var measure = match.Groups["measure"].Value.Equals("spending", StringComparison.OrdinalIgnoreCase)
            ? "amount"
            : match.Groups["measure"].Value;

        if (!owner.Equals(group, StringComparison.OrdinalIgnoreCase) ||
            record.Equals(group, StringComparison.OrdinalIgnoreCase) ||
            record.Equals("for", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var records = $"records = [{{\"{group}\": \"Ada\", \"{measure}\": 25.0}}, {{\"{group}\": \"Grace\", \"{measure}\": 40.0}}, {{\"{group}\": \"Ada\", \"{measure}\": 15.0}}]";
        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", records),
            new TaskRequestStep("assign-variable", "assignment", "totals = {}"),
            new TaskRequestStep("begin-loop", "loop-header", $"{record} in records"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{group} = {record}[\"{group}\"]"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"totals[{group}] = totals.get({group}, 0) + {record}[\"{measure}\"]"),
            new TaskRequestStep("begin-loop", "loop-header", $"{group}, total in totals.items()"),
            new TaskRequestStep("write-standard-output-indented-expression", "expression", $"{group}, total"));
        return true;
    }

    private static bool TryGetDirectoryFileListingProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!DirectoryFileListingPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("import-module", "module-name", "os"),
            new TaskRequestStep("import-module", "module-name", "sys"),
            new TaskRequestStep("assign-variable", "assignment", "directory = sys.argv[1]"),
            new TaskRequestStep("begin-try", "statement", "try"),
            new TaskRequestStep("begin-loop-indented", "loop-header", "entry in sorted(os.scandir(directory), key=lambda entry: entry.name)"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "entry.is_file()"),
            new TaskRequestStep("write-standard-output-triple-indented-expression", "expression", "f\"{entry.name}: {entry.stat().st_size} bytes\""),
            new TaskRequestStep("begin-except", "exception-type", "FileNotFoundError"),
            new TaskRequestStep("write-standard-output-indented-expression", "expression", "f\"Directory not found: {directory}\""));
        return true;
    }

    private static bool TryGetNonEmptyFileLineCountProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!NonEmptyFileLineCountPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "line_count = 0"),
            new TaskRequestStep("begin-with", "expression", "open(\"input.txt\", \"r\", encoding=\"utf-8\") as input_file"),
            new TaskRequestStep("begin-loop-indented", "loop-header", "line in input_file"),
            new TaskRequestStep("begin-conditional-double-indented", "condition", "line.strip()"),
            new TaskRequestStep("assign-variable-triple-indented", "assignment", "line_count += 1"),
            new TaskRequestStep("write-standard-output", "expression", "line_count"));
        return true;
    }

    private static bool TryGetLoopFilterProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = LoopFilterPattern.Match(source);
        if (!match.Success ||
            !TryNormalizeNumericListLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        var item = match.Groups["item"].Value;
        var variable = item.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? item[..^1]
            : item;
        var parity = match.Groups["parity"].Value.Equals("even", StringComparison.OrdinalIgnoreCase)
            ? "=="
            : "!=";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", $"{variable} in {items}"),
            new TaskRequestStep(
                "begin-conditional-indented",
                "condition",
                $"{variable} % 2 {parity} 0"),
            new TaskRequestStep(
                "write-standard-output-double-indented-expression",
                "expression",
                variable));
        return true;
    }

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

    private static bool TryGetGenericEvenListFilterProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!GenericEvenListFilterPattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", "number in [1, 2, 3, 4, 5, 6]"),
            new TaskRequestStep("begin-conditional-indented", "condition", "number % 2 == 0"),
            new TaskRequestStep("write-standard-output-double-indented-expression", "expression", "number"));
        return true;
    }

    private static bool TryGetBuiltInSumAvoidanceProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        if (!BuiltInSumAvoidancePattern.IsMatch(source))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", "total = 0"),
            new TaskRequestStep("begin-loop", "loop-header", "number in [1, 2, 3, 4, 5]"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                "total = total + number"),
            new TaskRequestStep("write-standard-output", "expression", "total"));
        return true;
    }

    private static bool TryGetLoopAccumulatorProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = LoopAccumulatorPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var total = match.Groups["total"].Value;
        var target = match.Groups["target"].Value;
        var item = match.Groups["item"].Value;
        var output = match.Groups["output"].Value;
        if (!total.Equals(target, StringComparison.OrdinalIgnoreCase) ||
            !total.Equals(output, StringComparison.OrdinalIgnoreCase) ||
            total.Equals(item, StringComparison.OrdinalIgnoreCase) ||
            !TryNormalizeNumericListLiteral(match.Groups["items"].Value, out var items))
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("assign-variable", "assignment", $"{total} = 0"),
            new TaskRequestStep("begin-loop", "loop-header", $"{item} in {items}"),
            new TaskRequestStep(
                "assign-variable-indented",
                "assignment",
                $"{total} = {total} + {item}"),
            new TaskRequestStep("write-standard-output", "expression", total));
        return true;
    }

    private static bool TryNormalizeSimpleDictionaryLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string key = @"(?:'[^'\\]*'|""[^""\\]*"")";
        const string number = @"[+-]?\d+(?:\.\d+)?";
        if (!Regex.IsMatch(
            normalized,
            $@"^\{{\s*{key}\s*:\s*{number}(?:\s*,\s*{key}\s*:\s*{number})*\s*\}}$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*:\s*", ": ");
        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

    private static bool TryNormalizeNumericListLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string number = @"[+-]?\d+(?:\.\d+)?";
        if (!Regex.IsMatch(
            normalized,
            $@"^\[\s*{number}(?:\s*,\s*{number})*\s*\]$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

    private static bool TryGetLoopProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var collectionMatch = LoopCollectionOutputPattern.Match(source);
        if (collectionMatch.Success &&
            TryNormalizeListLiteral(collectionMatch.Groups["items"].Value, out var items))
        {
            var item = collectionMatch.Groups["item"].Value;
            var variable = item.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? item[..^1]
                : item;

            steps = ImmutableArray.Create(
                new TaskRequestStep("begin-loop", "loop-header", $"{variable} in {items}"),
                new TaskRequestStep(
                    "write-standard-output-indented-expression",
                    "expression",
                    variable));
            return true;
        }

        var match = LoopRangeOutputPattern.Match(source);
        if (!match.Success ||
            !int.TryParse(match.Groups["start"].Value, out var start) ||
            !int.TryParse(match.Groups["end"].Value, out var inclusiveEnd) ||
            inclusiveEnd < start ||
            inclusiveEnd == int.MaxValue)
        {
            return false;
        }

        var rangeVariable = match.Groups["item"].Value.ToLowerInvariant() switch
        {
            "numbers" => "number",
            "values" => "value",
            "items" => "item",
            "counts" => "count",
            var item => item
        };
        var loopHeader = $"{rangeVariable} in range({start}, {inclusiveEnd + 1})";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", loopHeader),
            new TaskRequestStep(
                "write-standard-output-indented-expression",
                "expression",
                rangeVariable));
        return true;
    }

    private static bool TryNormalizeListLiteral(string value, out string normalized)
    {
        normalized = value.Trim();
        const string number = @"[+-]?\d+(?:\.\d+)?";
        const string singleQuotedString = @"'(?:\\.|[^'\\])*'";
        const string doubleQuotedString = @"""(?:\\.|[^""\\])*""";
        var item = $@"(?:{number}|{singleQuotedString}|{doubleQuotedString})";
        if (!Regex.IsMatch(
            normalized,
            $@"^\[\s*{item}(?:\s*,\s*{item})*\s*\]$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        normalized = Regex.Replace(normalized, @"\s*,\s*", ", ");
        return true;
    }

    private static bool TryGetConditionalProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = ConditionalOutputPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var comparison = Regex.Replace(match.Groups["operator"].Value, @"\s+", " ")
            .Trim()
            .ToLowerInvariant();
        var symbol = comparison switch
        {
            "is at least" or "at least" => ">=",
            "is at most" or "at most" => "<=",
            "is greater than" or "greater than" => ">",
            "is less than" or "less than" => "<",
            "is not equal to" or "not equal to" => "!=",
            "is equal to" or "equal" or "equals" => "==",
            _ => string.Empty
        };

        if (symbol.Length == 0)
        {
            return false;
        }

        var condition = $"{match.Groups["left"].Value} {symbol} {match.Groups["right"].Value}";
        var output = match.Groups["text"].Value.Trim();
        if (output.Length == 0)
        {
            return false;
        }

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-conditional", "condition", condition),
            new TaskRequestStep("write-standard-output-indented", "text", output));
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
