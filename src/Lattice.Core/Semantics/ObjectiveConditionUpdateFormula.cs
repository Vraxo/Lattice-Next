using System.Collections.Immutable;

namespace Lattice.Core.Semantics;

public sealed record ObjectiveConditionUpdateFormula(ImmutableArray<ObjectiveCondition> Conditions) : SemanticFormula;
