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
            if (assignmentTarget is null ||
                !assignmentTarget.Values.TryGetValue("name", out var variableName) ||
                string.IsNullOrWhiteSpace(variableName) ||
                assignmentValue is null ||
                !assignmentValue.Expressions.TryGetValue("value", out var value))
            {
                error = "A generic assignment requires a captured variable name and typed value expression.";
                return false;
            }

            requirements.Add(new AssignmentRequirement(variableName, value));
        }

        if (requirements.Count == 0)
        {
            error = "No supported generic scalar program requirements were found.";
            return false;
        }

        program = new SemanticProgram(requirements.ToImmutable());
        return true;
    }
}
