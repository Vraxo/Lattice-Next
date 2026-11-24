using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>Projects generic request facts into the language-neutral program requirement model.</summary>
public static class SemanticProgramFactMapper
{
    public static bool TryCreate(
        ImmutableArray<SequenceSemanticFact> facts,
        out SemanticProgram? program,
        out string? error)
    {
        program = null;
        error = null;
        if (facts.IsDefault)
        {
            error = "Semantic program facts must be initialized.";
            return false;
        }

        var requirements = ImmutableArray.CreateBuilder<ProgramRequirement>();
        var assignmentTarget = facts.FirstOrDefault(fact => fact.Id == "program.assignment.target");
        var assignmentValue = facts.FirstOrDefault(fact => fact.Id == "program.assignment.value");
        if (assignmentTarget is not null || assignmentValue is not null)
        {
            if (assignmentTarget is null || assignmentValue is null ||
                !TryGetNames(assignmentTarget, out var variableNames) ||
                !TryGetExpressions(assignmentValue, out var values) ||
                variableNames.IsDefaultOrEmpty ||
                variableNames.Length != values.Length ||
                variableNames.Any(string.IsNullOrWhiteSpace))
            {
                error = "A generic assignment requires aligned variable names and typed value expressions.";
                return false;
            }

            for (var index = 0; index < variableNames.Length; index++)
            {
                requirements.Add(new AssignmentRequirement(variableNames[index], values[index]));
            }
        }

        if (requirements.Count == 0)
        {
            error = "No supported generic scalar program requirements were found.";
            return false;
        }

        program = new SemanticProgram(requirements.ToImmutable());
        return true;
    }

    private static bool TryGetNames(
        SequenceSemanticFact fact,
        out ImmutableArray<string> values)
    {
        if (fact.Values.TryGetValue("name", out var value))
        {
            values = [value];
            return true;
        }

        return fact.Collections.TryGetValue("name", out values);
    }

    private static bool TryGetExpressions(
        SequenceSemanticFact fact,
        out ImmutableArray<SemanticExpression> expressions)
    {
        if (fact.Expressions.TryGetValue("value", out var expression))
        {
            expressions = [expression];
            return true;
        }

        return fact.ExpressionCollections.TryGetValue("value", out expressions);
    }
}
