using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgeBase
{
    private readonly List<KnowledgeFact> facts = [];
    private readonly List<UniversalRule> rules = [];

    public IReadOnlyList<KnowledgeFact> Facts => facts;

    public IReadOnlyList<UniversalRule> Rules => rules;

    public bool TryAdd(
        SemanticUtterance utterance,
        out KnowledgeEntryKind kind,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        kind = default;
        error = null;

        if (utterance.Kind != UtteranceKind.Assertion)
        {
            error = "Questions cannot be added to the knowledge base.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(utterance.SourceText))
        {
            error = "The source sentence is missing.";
            return false;
        }

        if (utterance.Meaning is PredicateApplication predicate &&
            TryGetIndividual(predicate, out _))
        {
            if (!facts.Any(fact => IsSameFact(fact.Predicate, predicate)))
            {
                facts.Add(new KnowledgeFact(predicate, utterance.SourceText));
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (utterance.Meaning is UniversalQuantification universal &&
            universal.Body is Implication implication &&
            universal.Variable is { } variable &&
            implication.Premise is PredicateApplication premise &&
            implication.Conclusion is PredicateApplication conclusion &&
            IsBoundUnaryPredicate(premise, variable) &&
            IsBoundUnaryPredicate(conclusion, variable))
        {
            if (!rules.Any(rule =>
                    rule.Premise.Predicate == premise.Predicate &&
                    rule.Conclusion.Predicate == conclusion.Predicate))
            {
                rules.Add(new UniversalRule(premise, conclusion, utterance.SourceText));
            }

            kind = KnowledgeEntryKind.Rule;
            return true;
        }

        error =
            "Only unary individual facts and single-premise universal rules are supported by the current reasoning engine.";
        return false;
    }

    private static bool TryGetIndividual(
        PredicateApplication predicate,
        out SemanticIdentifier individual)
    {
        individual = default;

        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefault ||
            predicate.Arguments.Length != 1 ||
            predicate.Arguments[0] is not IndividualTerm term ||
            string.IsNullOrWhiteSpace(term.Identifier.Value))
        {
            return false;
        }

        individual = term.Identifier;
        return true;
    }

    private static bool IsBoundUnaryPredicate(
        PredicateApplication predicate,
        VariableTerm variable) =>
        !string.IsNullOrWhiteSpace(predicate.Predicate.Value) &&
        !string.IsNullOrWhiteSpace(variable.Identifier.Value) &&
        !predicate.Arguments.IsDefault &&
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] is VariableTerm argument &&
        argument.Identifier == variable.Identifier;

    private static bool IsSameFact(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        TryGetIndividual(left, out var leftIndividual) &&
        TryGetIndividual(right, out var rightIndividual) &&
        leftIndividual == rightIndividual;
}
