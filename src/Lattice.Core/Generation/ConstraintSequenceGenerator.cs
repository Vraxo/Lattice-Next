using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceRequirement(
    string Id,
    ImmutableArray<string> Tokens,
    ImmutableArray<string> DependsOn);

public sealed record SequenceConstraintGenerationRequest(
    ImmutableArray<string> ContextTokens,
    ImmutableArray<SequenceRequirement> Requirements,
    int BeamWidth = 8,
    int MaxTokens = 512);

public static class ConstraintSequenceGenerator
{
    private sealed record GenerationState(
        ImmutableArray<int> Progress,
        ImmutableArray<string> Context,
        ImmutableArray<string> Tokens,
        double Score);

    public static bool TryGenerate(
        SequenceTokenModel model,
        SequenceConstraintGenerationRequest request,
        out SequenceGenerationResult? result,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        result = null;
        error = null;

        if (!TryValidate(request, out var requirementIndexes, out var requiredTokenCount, out error))
        {
            return false;
        }

        if (request.BeamWidth is < 1 or > 128 ||
            request.MaxTokens is < 1 or > 8192 ||
            requiredTokenCount > request.MaxTokens)
        {
            error = "The sequence constraints or generation limits exceed supported bounds.";
            return false;
        }

        var frontier = new List<GenerationState>
        {
            new(
                Enumerable.Repeat(0, request.Requirements.Length).ToImmutableArray(),
                request.ContextTokens,
                ImmutableArray<string>.Empty,
                0)
        };
        var completed = new List<GenerationState>();
        var steps = 0;

        for (; steps < request.MaxTokens && frontier.Count > 0; steps++)
        {
            var next = new List<GenerationState>();
            foreach (var state in frontier)
            {
                if (IsComplete(state.Progress, request.Requirements))
                {
                    completed.Add(state);
                    continue;
                }

                for (var requirementIndex = 0;
                    requirementIndex < request.Requirements.Length;
                    requirementIndex++)
                {
                    var requirement = request.Requirements[requirementIndex];
                    var progress = state.Progress[requirementIndex];
                    if (progress >= requirement.Tokens.Length ||
                        !DependenciesAreComplete(requirement, request.Requirements, state.Progress, requirementIndexes))
                    {
                        continue;
                    }

                    var token = requirement.Tokens[progress];
                    var score = state.Score + model.GetLogScore(state.Context, token);
                    if (!double.IsFinite(score))
                    {
                        continue;
                    }

                    var nextContext = state.Context.Add(token);
                    if (nextContext.Length > model.MaximumContextLength)
                    {
                        nextContext = model.MaximumContextLength == 0
                            ? ImmutableArray<string>.Empty
                            : nextContext
                                .Skip(nextContext.Length - model.MaximumContextLength)
                                .ToImmutableArray();
                    }

                    next.Add(new GenerationState(
                        state.Progress.SetItem(requirementIndex, progress + 1),
                        nextContext,
                        state.Tokens.Add(token),
                        score));
                }
            }

            if (completed.Count > request.BeamWidth * 4)
            {
                completed = completed
                    .OrderByDescending(state => state.Score)
                    .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
                    .Take(request.BeamWidth * 4)
                    .ToList();
            }

            frontier = next
                .OrderByDescending(state => state.Score)
                .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
                .ThenBy(state => BuildProgressKey(state.Progress), StringComparer.Ordinal)
                .Take(request.BeamWidth)
                .ToList();
        }

        completed.AddRange(frontier.Where(state => IsComplete(state.Progress, request.Requirements)));
        if (completed.Count == 0)
        {
            error = steps >= request.MaxTokens
                ? "Constrained generation reached its token limit before satisfying every requirement."
                : "No valid token sequence satisfies all active requirements.";
            return false;
        }

        var best = completed
            .OrderByDescending(state => state.Score)
            .ThenBy(state => BuildTokenKey(state.Tokens), StringComparer.Ordinal)
            .First();

        result = new SequenceGenerationResult(
            best.Tokens,
            best.Score,
            steps >= request.MaxTokens && frontier.Any(state =>
                !IsComplete(state.Progress, request.Requirements)));
        return true;
    }

    private static bool TryValidate(
        SequenceConstraintGenerationRequest request,
        out ImmutableDictionary<string, int> requirementIndexes,
        out int requiredTokenCount,
        out string? error)
    {
        requirementIndexes = ImmutableDictionary<string, int>.Empty;
        requiredTokenCount = 0;
        error = null;

        if (request.ContextTokens.IsDefault ||
            request.ContextTokens.Length > 8192 ||
            request.ContextTokens.Any(string.IsNullOrEmpty) ||
            request.Requirements.IsDefaultOrEmpty ||
            request.Requirements.Length > 128)
        {
            error = "A valid context and one to 128 initialized sequence requirements are required.";
            return false;
        }

        var indexes = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < request.Requirements.Length; index++)
        {
            var requirement = request.Requirements[index];
            if (requirement is null ||
                string.IsNullOrWhiteSpace(requirement.Id) ||
                requirement.Id.Length > 128 ||
                requirement.Tokens.IsDefaultOrEmpty ||
                requirement.Tokens.Any(string.IsNullOrEmpty) ||
                (requirement.DependsOn.IsDefault
                    ? false
                    : requirement.DependsOn.Any(string.IsNullOrWhiteSpace)) ||
                !indexes.TryAdd(requirement.Id, index))
            {
                error = "Every requirement needs a unique identifier and a non-empty sequence of valid tokens.";
                return false;
            }

            requiredTokenCount += requirement.Tokens.Length;
            if (requiredTokenCount > 8192)
            {
                error = "The combined required token sequence exceeds the supported limit.";
                return false;
            }
        }

        requirementIndexes = indexes.ToImmutable();
        foreach (var requirement in request.Requirements)
        {
            var dependencies = requirement.DependsOn.IsDefault
                ? ImmutableArray<string>.Empty
                : requirement.DependsOn;
            if (dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Length ||
                dependencies.Any(dependency =>
                    dependency == requirement.Id || !requirementIndexes.ContainsKey(dependency)))
            {
                error = $"Requirement '{requirement.Id}' has a duplicate, self, or unknown dependency.";
                return false;
            }
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in request.Requirements)
        {
            if (HasDependencyCycle(requirement.Id, request.Requirements, visiting, visited, requirementIndexes))
            {
                error = "The sequence requirements contain a dependency cycle.";
                return false;
            }
        }

        return true;
    }

    private static bool HasDependencyCycle(
        string id,
        ImmutableArray<SequenceRequirement> requirements,
        HashSet<string> visiting,
        HashSet<string> visited,
        ImmutableDictionary<string, int> indexes)
    {
        if (visited.Contains(id))
        {
            return false;
        }

        if (!visiting.Add(id))
        {
            return true;
        }

        var requirement = requirements[indexes[id]];
        var dependencies = requirement.DependsOn.IsDefault
            ? ImmutableArray<string>.Empty
            : requirement.DependsOn;
        foreach (var dependency in dependencies)
        {
            if (HasDependencyCycle(dependency, requirements, visiting, visited, indexes))
            {
                return true;
            }
        }

        visiting.Remove(id);
        visited.Add(id);
        return false;
    }

    private static bool DependenciesAreComplete(
        SequenceRequirement requirement,
        ImmutableArray<SequenceRequirement> requirements,
        ImmutableArray<int> progress,
        ImmutableDictionary<string, int> indexes)
    {
        if (requirement.DependsOn.IsDefaultOrEmpty)
        {
            return true;
        }

        return requirement.DependsOn.All(dependency =>
        {
            var index = indexes[dependency];
            return progress[index] == requirements[index].Tokens.Length;
        });
    }

    private static bool IsComplete(
        ImmutableArray<int> progress,
        ImmutableArray<SequenceRequirement> requirements)
    {
        for (var index = 0; index < requirements.Length; index++)
        {
            if (progress[index] != requirements[index].Tokens.Length)
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildTokenKey(ImmutableArray<string> tokens) =>
        string.Join("\u001f", tokens);

    private static string BuildProgressKey(ImmutableArray<int> progress) =>
        string.Join("\u001f", progress);
}
