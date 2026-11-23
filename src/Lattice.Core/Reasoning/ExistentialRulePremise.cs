using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed record ExistentialRulePremise(
    VariableTerm Witness,
    ImmutableArray<PredicateApplication> Conditions,
    ImmutableArray<PredicateApplication> Relations);
