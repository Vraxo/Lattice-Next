using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// A parsed English noun description shared by quantified, existential, and definite noun phrases.
/// </summary>
internal sealed record EnglishDescription(
    string Head,
    ImmutableArray<string> Modifiers,
    ImmutableArray<string> RelativeProperties)
{
    public SemanticFormula ToUniversalConditions(VariableTerm variable)
    {
        var predicates = Modifiers
            .AddRange(RelativeProperties)
            .Add(Head)
            .Select(modifier => (SemanticFormula)new PredicateApplication(
                new SemanticIdentifier(modifier),
                ImmutableArray.Create<SemanticTerm>(variable)))
            .ToImmutableArray();

        return predicates.Length == 1
            ? predicates[0]
            : new Conjunction(predicates);
    }

    public SemanticFormula ToExistentialConditions(VariableTerm variable)
    {
        var predicates = Modifiers
            .Add(Head)
            .AddRange(RelativeProperties)
            .Select(modifier => (SemanticFormula)new PredicateApplication(
                new SemanticIdentifier(modifier),
                ImmutableArray.Create<SemanticTerm>(variable)))
            .ToImmutableArray();

        return predicates.Length == 1
            ? predicates[0]
            : new Conjunction(predicates);
    }

    public DescriptiveIndividualTerm ToDefiniteTerm() =>
        new(
            new SemanticIdentifier(Head),
            Modifiers
                .AddRange(RelativeProperties)
                .Select(modifier => new SemanticIdentifier(modifier))
                .ToImmutableArray());
}
