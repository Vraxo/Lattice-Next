using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectiveFormula(string Goal) : SemanticFormula
{
    public ImmutableArray<ObjectiveCondition> Conditions { get; init; } =
        ImmutableArray<ObjectiveCondition>.Empty;
}
