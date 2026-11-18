using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonFunctionProgramParser
{
    private static readonly Regex GreetingFunctionPattern = new(
    @"\b(?:defines?|creates?)\s+(?:a\s+)?function(?:\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s+that\s+(?:accepts?|takes?)\s+(?:a\s+)?person'?s\s+name\s+and\s+returns?\s+(?:a\s+)?greeting\b.*?\bcall\s+(?:it|the\s+function)\s+with\s+two\s+different\s+names\b",
    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

    internal static bool TryGetFunctionCompositionProgram(
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

    internal static bool TryGetFunctionMultiConditionalProgram(
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

    internal static bool TryGetFunctionConditionalProgram(
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

    internal static bool TryGetFunctionWithLocalVariableProgram(
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

    internal static bool TryGetGreetingFunctionProgram(
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

    internal static bool TryGetFunctionProgram(
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
}
