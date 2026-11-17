using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Lattice.Client;
using Lattice.Core.Generation;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;

internal static class TaskRequestResponderHelpers
{
    internal static string EnsureSentenceEnding(string value)
    {
        return value.TrimEnd().TrimEnd('.', '!', '?') + ".";
    }

    internal static bool IsSubtype(string candidate, string expected, SemanticWorldModel model)
    {
        if (candidate == expected)
        {
            return true;
        }

        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(candidate);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var parent in model.RelationAssertions
                .Where(assertion => assertion.Subject == current && assertion.Relation == "is-a")
                .Select(assertion => assertion.Object))
            {
                if (parent == expected)
                {
                    return true;
                }

                pending.Push(parent);
            }
        }

        return false;
    }

    internal static LatticeEvidenceSource ToEvidenceSource(SemanticWorldModelSource source) =>
        new(
            LatticeKnowledgeSourceKind.FoundationalKnowledge,
            source.SourceId,
            source.SourceId,
            null,
            null);

    internal static string IndefinitePhrase(string value) =>
        value.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(value[0]))
            ? $"an {value}"
            : $"a {value}";

    internal static string JoinNames(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => string.Empty,
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
        };

    internal static bool Fail(
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        return false;
    }
}
