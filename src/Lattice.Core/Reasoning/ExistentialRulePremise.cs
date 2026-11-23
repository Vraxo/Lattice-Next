using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ExistentialRulePremise(
    VariableTerm Witness,
    ImmutableArray<PredicateApplication> Conditions,
    ImmutableArray<PredicateApplication> NegativeConditions,
    ImmutableArray<PredicateApplication> Relations,
    ImmutableArray<PredicateApplication> VariableRelations,
    ImmutableArray<PredicateApplication> FixedObjectRelations,
    ImmutableArray<ExistentialRulePremise> NestedPremises);
