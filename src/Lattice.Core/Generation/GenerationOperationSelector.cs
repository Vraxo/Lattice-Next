using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed class GenerationOperationSelector
{
    public bool TrySelect(
        ImmutableArray<GenerationOperation> operations,
        GenerationGoal goal,
        out GenerationOperation? selected,
        out ImmutableArray<GenerationCandidateAssessment> assessments,
        out string? error)
    {
        selected = null;
        assessments = ImmutableArray<GenerationCandidateAssessment>.Empty;
        error = null;

        if (operations.IsDefaultOrEmpty ||
            string.IsNullOrWhiteSpace(goal.Effect) ||
            string.IsNullOrWhiteSpace(goal.ArgumentKind) ||
            string.IsNullOrWhiteSpace(goal.ArgumentValue) ||
            goal.AvailableCapabilities.IsDefault)
        {
            error = "An operation selection requires operations and a complete semantic goal.";
            return false;
        }

        var available = goal.AvailableCapabilities.ToHashSet(StringComparer.Ordinal);
        var evaluated = ImmutableArray.CreateBuilder<GenerationCandidateAssessment>(operations.Length);
        var eligible = new List<GenerationOperation>();

        foreach (var operation in operations)
        {
            if (!string.Equals(operation.Effect, goal.Effect, StringComparison.Ordinal))
            {
                evaluated.Add(new GenerationCandidateAssessment(
                    operation.Id,
                    false,
                    $"It does not produce the required effect '{goal.Effect}'.",
                    operation.Prerequisites.Length));
                continue;
            }

            if (!string.Equals(operation.ArgumentKind, goal.ArgumentKind, StringComparison.Ordinal))
            {
                evaluated.Add(new GenerationCandidateAssessment(
                    operation.Id,
                    false,
                    $"It does not accept the required argument kind '{goal.ArgumentKind}'.",
                    operation.Prerequisites.Length));
                continue;
            }

            var missing = operation.Prerequisites
                .Where(prerequisite => !available.Contains(prerequisite))
                .ToArray();
            if (missing.Length > 0)
            {
                evaluated.Add(new GenerationCandidateAssessment(
                    operation.Id,
                    false,
                    $"Unavailable prerequisites: {string.Join(", ", missing)}.",
                    operation.Prerequisites.Length));
                continue;
            }

            eligible.Add(operation);
            evaluated.Add(new GenerationCandidateAssessment(
                operation.Id,
                true,
                "It satisfies the requested effect and argument kind, and all prerequisites are available.",
                operation.Prerequisites.Length));
        }

        assessments = evaluated.ToImmutable();
        selected = eligible
            .OrderBy(operation => operation.Prerequisites.Length)
            .ThenBy(operation => operation.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (selected is null)
        {
            error = "No known operation can satisfy the requested effect with the available argument and capabilities.";
            return false;
        }

        return true;
    }
}
