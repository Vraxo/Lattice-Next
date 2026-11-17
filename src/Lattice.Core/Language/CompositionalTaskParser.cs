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
 
    private static readonly Regex FunctionCompositionProgramPattern = new(
        @"\\bfunction\\s+named\\s+(?<firstName>[A-Za-z_][A-Za-z0-9_]*)\\s+that\\s+takes\\s+(?<firstParameters>[A-Za-z_][A-Za-z0-9_]*(?:\\s+and\\s+[A-Za-z_][A-Za-z0-9_]*)*)\\s+and\\s+returns\\s+(?<firstExpression>.+?),\\s*(?:and\\s+)?(?:a\\s+)?function\\s+named\\s+(?<secondName>[A-Za-z_][A-Za-z0-9_]*)\\s+that\\s+takes\\s+(?<secondParameters>[A-Za-z_][A-Za-z0-9_]*(?:\\s+and\\s+[A-Za-z_][A-Za-z0-9_]*)*)\\s+and\\s+returns\\s+(?<secondExpression>.+?)\\s*,?\\s+then\\s+(?:print|display|output)\\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\\s*\\(\\s*[^()]+\\s*\\))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<expression>.+?)\s*,?\s+then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ConditionalOutputPattern = new(
        @"\b(?:print|display|show|output)s?\s+(?<quote>['""])(?<text>[^'""]+)\k<quote>\s+if\s+(?<left>\d+(?:\.\d+)?)\s+(?<operator>is\s+at\s+least|at\s+least|is\s+at\s+most|at\s+most|is\s+greater\s+than|greater\s+than|is\s+less\s+than|less\s+than|is\s+equal\s+to|equals?)\s+(?<right>\d+(?:\.\d+)?)\b",
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
        else if (TryGetFunctionProgram(source, out var functionSteps))
        {
            formula = formula with { GenerationSteps = functionSteps };
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
        var match = FunctionCompositionProgramPattern.Match(source);
        if (!match.Success)
        {
            return false;
        }

        var firstName = match.Groups["firstName"].Value;
        var firstParameters = match.Groups["firstParameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var firstExpression = match.Groups["firstExpression"].Value.Trim();
        var secondName = match.Groups["secondName"].Value;
        var secondParameters = match.Groups["secondParameters"].Value
            .Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var secondExpression = match.Groups["secondExpression"].Value.Trim();
        var compactCall = Regex.Replace(match.Groups["call"].Value, @"\\s+", string.Empty);
        var numericArguments = string.Join(",", Enumerable.Repeat(@"[+-]?\\d+(?:\\.\\d+)?", secondParameters.Length));

        if (firstName.Equals(secondName, StringComparison.OrdinalIgnoreCase) ||
            !IsArithmeticExpression(firstExpression) ||
            !IsArithmeticExpression(secondExpression) ||
            !Regex.IsMatch(
                secondExpression,
                $@"\\b{Regex.Escape(firstName)}\\s*\\(",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) ||
            !Regex.IsMatch(
                compactCall,
                $@"^{Regex.Escape(secondName)}\\({numericArguments}\\)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return false;
        }

        var callName = compactCall[..compactCall.IndexOf('(')];
        var callArguments = compactCall[(compactCall.IndexOf('(') + 1)..^1].Split(',');
        var call = $"{callName}({string.Join(", ", callArguments)})";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-function", "function-header", $"{firstName}({string.Join(", ", firstParameters)})"),
            new TaskRequestStep("return-value-indented-expression", "expression", firstExpression),
            new TaskRequestStep("begin-function", "function-header", $"{secondName}({string.Join(", ", secondParameters)})"),
            new TaskRequestStep("return-value-indented-expression", "expression", secondExpression),
            new TaskRequestStep("write-standard-output", "expression", call));
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

    private static bool TryGetLoopProgram(
        string source,
        out ImmutableArray<TaskRequestStep> steps)
    {
        steps = ImmutableArray<TaskRequestStep>.Empty;
        var match = LoopRangeOutputPattern.Match(source);
        if (!match.Success ||
            !int.TryParse(match.Groups["start"].Value, out var start) ||
            !int.TryParse(match.Groups["end"].Value, out var inclusiveEnd) ||
            inclusiveEnd < start ||
            inclusiveEnd == int.MaxValue)
        {
            return false;
        }

        var variable = match.Groups["item"].Value.ToLowerInvariant() switch
        {
            "numbers" => "number",
            "values" => "value",
            "items" => "item",
            "counts" => "count",
            var item => item
        };
        var loopHeader = $"{variable} in range({start}, {inclusiveEnd + 1})";

        steps = ImmutableArray.Create(
            new TaskRequestStep("begin-loop", "loop-header", loopHeader),
            new TaskRequestStep(
                "write-standard-output-indented-expression",
                "expression",
                variable));
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
