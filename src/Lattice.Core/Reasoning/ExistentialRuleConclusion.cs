using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ExistentialRuleConclusion(
    VariableTerm Witness,
    ImmutableArray<PredicateApplication> Conditions,
    PredicateApplication Relation);
