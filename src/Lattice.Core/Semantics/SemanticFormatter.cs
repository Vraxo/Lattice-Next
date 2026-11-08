namespace Lattice.Core.Semantics;

public static class SemanticFormatter
{
    public static string Format(SemanticUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        var meaning = Format(utterance.Meaning);

        return utterance.Kind switch
        {
            UtteranceKind.Assertion => meaning,
            UtteranceKind.Question => $"ask({meaning})",
            _ => throw new ArgumentOutOfRangeException(nameof(utterance), utterance.Kind, "Unsupported utterance kind.")
        };
    }

    private static string Format(SemanticFormula formula) =>
        formula switch
        {
            PredicateApplication predicate => FormatPredicate(predicate),
            Implication implication => $"implies({Format(implication.Premise)}, {Format(implication.Conclusion)})",
            UniversalQuantification universal => $"forall({Format(universal.Variable)}, {Format(universal.Body)})",
            _ => throw new ArgumentOutOfRangeException(nameof(formula), formula, "Unsupported semantic formula.")
        };

    private static string FormatPredicate(PredicateApplication predicate)
    {
        var arguments = string.Join(", ", predicate.Arguments.Select(Format));
        return $"{predicate.Predicate}({arguments})";
    }

    private static string Format(SemanticTerm term) =>
        term switch
        {
            IndividualTerm individual => individual.Identifier.Value,
            VariableTerm variable => variable.Identifier.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(term), term, "Unsupported semantic term.")
        };
}
