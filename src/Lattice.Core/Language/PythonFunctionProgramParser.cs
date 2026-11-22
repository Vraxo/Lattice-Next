using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class PythonFunctionProgramParser
{
    private static readonly Regex FunctionDefinitionPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<expression>.+?)(?=,\s*(?:and\s+)?(?:a\s+)?function\s+named\b|,\s*then\s+(?:print|display|output)\b)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionCompositionCallPattern = new(
        @"\bthen\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionMultiConditionalProgramPattern = new(
        @"\bfunction\s+named\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+that\s+takes\s+(?<parameters>[A-Za-z_][A-Za-z0-9_]*(?:\s+and\s+[A-Za-z_][A-Za-z0-9_]*)*)\s+and\s+returns\s+(?<branchValue>.+?)\s+when\s+(?<branchCondition>.+?)(?:,\s*(?:and\s+)?returns\s+(?<branchValue>.+?)\s+when\s+(?<branchCondition>.+?))*\s*,?\s+and\s+otherwise\s+returns\s+(?<falseValue>.+?),?\s+then\s+(?:print|display|output)\s+(?<call>[A-Za-z_][A-Za-z0-9_]*\s*\(\s*[^()]+\s*\))\s*[.!?]?\s*$",
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


}
