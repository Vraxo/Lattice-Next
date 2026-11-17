using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class CompositionalTaskParser
{
    private static readonly HashSet<string> CreationVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "create", "generate", "make", "build"
    };

    private static readonly HashSet<string> CodeArtifacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "program", "script", "application", "code"
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


    internal static bool TryParse(
        string source,
        string sourceText,
        out SemanticUtterance? utterance)
    {
        utterance = null;

        if (!EnglishSyntaxParser.TryParse(source, out var syntax))
        {
            return false;
        }

        var creationClauseIndex = -1;
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

        var formula = new TaskRequestFormula(
            TaskRequestAction.Code,
            "python-program",
            "python",
            "python-source",
            "write-standard-output",
            ImmutableArray.Create("text-output"));

        if (TryGetFunctionCompositionProgram(source, out var compositionSteps))
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
        else if (TryGetFunctionProgram(source, out var functionSteps))
        {
            formula = formula with { GenerationSteps = functionSteps };
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
        else if (TryGetRequestedOutput(syntax, creationClauseIndex, out var requestedValue))
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


    private static bool IsArithmeticExpression(string expression)
    {
        var tokenPattern = @"[A-Za-z_][A-Za-z0-9_]*|\d+(?:\.\d+)?|[(),+\-*/%]";
        var tokens = Regex.Matches(expression, tokenPattern)
            .Select(match => match.Value)
            .ToArray();
        var remainder = Regex.Replace(expression, tokenPattern, string.Empty);
        if (tokens.Length == 0 || remainder.Any(character => !char.IsWhiteSpace(character)))
        {
            return false;
        }

        var index = 0;
        return TryParseAdditiveExpression(tokens, ref index) && index == tokens.Length;
    }

    private static bool TryParseAdditiveExpression(string[] tokens, ref int index)
    {
        if (!TryParseMultiplicativeExpression(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            if (!TryParseMultiplicativeExpression(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseMultiplicativeExpression(string[] tokens, ref int index)
    {
        if (!TryParseUnaryExpression(tokens, ref index))
        {
            return false;
        }

        while (index < tokens.Length && tokens[index] is "*" or "/" or "%")
        {
            index++;
            if (!TryParseUnaryExpression(tokens, ref index))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseUnaryExpression(string[] tokens, ref int index)
    {
        if (index < tokens.Length && tokens[index] is "+" or "-")
        {
            index++;
            return TryParseUnaryExpression(tokens, ref index);
        }

        return TryParsePrimaryExpression(tokens, ref index);
    }

    private static bool TryParsePrimaryExpression(string[] tokens, ref int index)
    {
        if (index >= tokens.Length)
        {
            return false;
        }

        if (tokens[index] == "(")
        {
            index++;
            if (!TryParseAdditiveExpression(tokens, ref index) ||
                index >= tokens.Length ||
                tokens[index] != ")")
            {
                return false;
            }

            index++;
            return true;
        }

        var token = tokens[index];
        if (Regex.IsMatch(
            token,
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant))
        {
            index++;
            if (index >= tokens.Length || tokens[index] != "(")
            {
                return true;
            }

            index++;
            if (!TryParseAdditiveExpression(tokens, ref index))
            {
                return false;
            }

            while (index < tokens.Length && tokens[index] == ",")
            {
                index++;
                if (!TryParseAdditiveExpression(tokens, ref index))
                {
                    return false;
                }
            }

            if (index >= tokens.Length || tokens[index] != ")")
            {
                return false;
            }

            index++;
            return true;
        }

        if (!Regex.IsMatch(
            token,
            @"^\d+(?:\.\d+)?$",
            RegexOptions.CultureInvariant))
        {
            return false;
        }

        index++;
        return true;
    }

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

            if (!IsArithmeticExpression(expression) ||
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
            !TryGetFunctionReturnExpression(match.Groups["falseValue"].Value, parameters, out var falseValue))
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
            if (!TryParseFunctionCondition(branchConditions[index], parameters, out var pythonCondition) ||
                !TryGetFunctionReturnExpression(branchValues[index], parameters, out var returnValue))
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

        if (!TryParseFunctionCondition(condition, parameters, out var pythonCondition) ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(name)}\({functionArguments}\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        if (!TryGetFunctionReturnExpression(match.Groups["trueValue"].Value, parameters, out var trueValue) ||
            !TryGetFunctionReturnExpression(match.Groups["falseValue"].Value, parameters, out var falseValue))
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

    private static bool TryParseFunctionCondition(
        string condition,
        string[] parameters,
        out string pythonCondition)
    {
        return TryParseBooleanCondition(condition, parameters, out pythonCondition);
    }

    private static bool TryParseBooleanCondition(
        string condition,
        string[] parameters,
        out string pythonCondition)
    {
        pythonCondition = string.Empty;
        var expression = condition.Trim();
        var isExplicitlyGrouped = false;
        while (HasEnclosingParentheses(expression))
        {
            isExplicitlyGrouped = true;
            expression = expression[1..^1].Trim();
        }

        foreach (var operation in new[] { "or", "and" })
        {
            var operatorIndex = FindTopLevelBooleanOperator(expression, operation);
            if (operatorIndex < 0)
            {
                continue;
            }

            var left = expression[..operatorIndex];
            var right = expression[(operatorIndex + operation.Length)..];
            if (!TryParseBooleanCondition(left, parameters, out var parsedLeft) ||
                !TryParseBooleanCondition(right, parameters, out var parsedRight))
            {
                return false;
            }

            var combined = $"{parsedLeft} {operation} {parsedRight}";
            pythonCondition = isExplicitlyGrouped ? $"({combined})" : combined;
            return true;
        }

        if (expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseBooleanCondition(expression[4..], parameters, out var negated))
            {
                return false;
            }

            var negatedExpression = $"not {negated}";
            pythonCondition = isExplicitlyGrouped ? $"({negatedExpression})" : negatedExpression;
            return true;
        }

        var match = Regex.Match(
            expression,
            @"^\s*(?<left>[A-Za-z_][A-Za-z0-9_]*)\s+(?<operator>is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+not\s+equal\s+to|not\s+equal\s+to|is\s+equal\s+to|equals?)\s+(?<right>.+?)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var leftOperand = match.Groups["left"].Value;
        var rightOperand = match.Groups["right"].Value;
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

        if (symbol.Length == 0 ||
            !parameters.Contains(leftOperand, StringComparer.OrdinalIgnoreCase) ||
            !TryNormalizeConditionOperand(rightOperand, parameters, out var normalizedRight))
        {
            return false;
        }

        var comparisonExpression = $"{leftOperand} {symbol} {normalizedRight}";
        pythonCondition = isExplicitlyGrouped ? $"({comparisonExpression})" : comparisonExpression;
        return true;
    }

    private static int FindTopLevelBooleanOperator(string expression, string operation)
    {
        var depth = 0;
        var quote = '\0';
        for (var index = 0; index <= expression.Length - operation.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '(')
            {
                depth++;
                continue;
            }

            if (character == ')')
            {
                depth--;
                continue;
            }

            if (depth == 0 &&
                string.Compare(expression, index, operation, 0, operation.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                index > 0 &&
                index + operation.Length < expression.Length &&
                char.IsWhiteSpace(expression[index - 1]) &&
                char.IsWhiteSpace(expression[index + operation.Length]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasEnclosingParentheses(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
        {
            return false;
        }

        var depth = 0;
        var quote = '\0';
        for (var index = 0; index < expression.Length; index++)
        {
            var character = expression[index];
            if (quote != '\0')
            {
                if (character == quote && (index == 0 || expression[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
                if (depth == 0 && index != expression.Length - 1)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }

    private static bool TryNormalizeConditionOperand(
        string operand,
        string[] parameters,
        out string normalized)
    {
        normalized = operand.Trim();
        if (normalized.Length >= 2 &&
            (normalized[0] == 39 || normalized[0] == 34) &&
            normalized[^1] == normalized[0])
        {
            normalized = ToPythonStringLiteral(normalized[1..^1]);
            return true;
        }

        if (!IsArithmeticExpression(normalized) ||
            Regex.Matches(normalized, @"[A-Za-z_][A-Za-z0-9_]*")
                .Any(identifier => !parameters.Contains(identifier.Value, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetFunctionReturnExpression(
        string value,
        string[] parameters,
        out string expression)
    {
        expression = value.Trim();
        if (expression.Length >= 2 &&
            expression[0] is '\'' or '"' &&
            expression[^1] == expression[0])
        {
            expression = ToPythonStringLiteral(expression[1..^1]);
            return true;
        }

        if (!IsArithmeticExpression(expression))
        {
            return false;
        }

        return Regex.Matches(expression, @"[A-Za-z_][A-Za-z0-9_]*")
            .All(match => parameters.Contains(match.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static string ToPythonStringLiteral(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

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
            assignments.Any(assignment => !IsArithmeticExpression(assignment)) ||
            !IsArithmeticExpression(expression) ||
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

        if (!IsArithmeticExpression(expression) ||
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
            !IsArithmeticExpression(expression))
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
        out string value)
    {
        var outputClause = syntax.Clauses
            .Skip(creationClauseIndex + 1)
            .FirstOrDefault(clause => OutputVerbs.Contains(clause.Verb));

        if (outputClause?.Object is not null)
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
