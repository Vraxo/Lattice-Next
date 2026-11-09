using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

/// <summary>
/// A parsed English noun description shared by quantified, existential, and definite noun phrases.
/// </summary>
internal sealed record EnglishDescription(
    string Head,
    ImmutableArray<string> Modifiers)
{
    public SemanticFormula ToConditions(VariableTerm variable)
    {
        var predicates = Modifiers
            .Add(Head)
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
            Modifiers.Select(modifier => new SemanticIdentifier(modifier)).ToImmutableArray());
}
