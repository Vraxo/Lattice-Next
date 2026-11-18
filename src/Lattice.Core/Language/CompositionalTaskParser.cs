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

    private static readonly Regex BankAccountClassPattern = new(
        @"\bclass\s+(?:representing|for)\s+(?:a\s+)?bank\s+account\b.*\bbalance\b.*\bdeposit(?:ion)?\s+method\b.*\bwithdraw(?:al)?\s+method\b.*\b(?:refuses?|rejects?)\s+withdrawals?\s+(?:that\s+are\s+)?(?:larger|greater)\s+than\s+(?:the\s+)?balance\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);


    private static readonly Regex BuiltInSumAvoidancePattern = new(
        @"\bcalculates?\s+(?:(?:their|the)\s+(?:total|sum)|(?:the\s+)?(?:total|sum)\s+of\s+(?:a|the)\s+list\s+of\s+numbers?)\s+without\s+using\s+(?:the\s+)?built[- ]in\s+sum\s+function\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoopAccumulatorPattern = new(
        @"\b(?:starts?|initializes?)\s+(?<total>[A-Za-z_][A-Za-z0-9_]*)\s+(?:at|to)\s+0\s+and\s+adds?\s+each\s+(?<item>[A-Za-z_][A-Za-z0-9_]*)\s+in\s+(?<items>\[[^\]]*\])\s+to\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*,?\s+then\s+(?:prints?|displays?|outputs?)\s+(?<output>[A-Za-z_][A-Za-z0-9_]*)",
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

        var isDirectoryListingRequest = PythonFileProgramParser.IsDirectoryListingRequest(source);
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
        else if (TryGetBankAccountClassProgram(source, out var bankAccountSteps))
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
