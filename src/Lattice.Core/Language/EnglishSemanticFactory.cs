using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

internal static class EnglishSemanticFactory
{
    internal static SemanticUtterance CreateUniversalUtterance(
        VariableTerm variable,
        SemanticFormula premise,
        SemanticFormula conclusion,
        string sourceText) =>
        new(
            UtteranceKind.Assertion,
            new UniversalQuantification(
                variable,
                new Implication(premise, conclusion)),
            sourceText);

    internal static ImmutableArray<SemanticFormula> GetConjuncts(SemanticFormula formula) =>
        formula is Conjunction conjunction
            ? conjunction.Conjuncts
            : ImmutableArray.Create(formula);

    internal static SemanticFormula Combine(IReadOnlyList<SemanticFormula> conditions) =>
        conditions.Count == 1
            ? conditions[0]
            : new Conjunction(conditions.ToImmutableArray());

    internal static PredicateApplication Predicate(
        string name,
        params SemanticTerm[] arguments) =>
        new(EnglishGrammarTokens.CreateIdentifier(name), ImmutableArray.CreateRange(arguments));
}
