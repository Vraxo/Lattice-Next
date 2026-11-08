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
            IsGroundPredicate(predicate))
        {
            if (!facts.Any(fact => IsSameFact(fact.Predicate, predicate)))
            {
                facts.Add(new KnowledgeFact(
                    predicate,
                    utterance.SourceText,
                    utterance.ResolvedReferent));
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
            "Only ground facts with individual or definite-description arguments and single-premise unary universal rules are supported by the current reasoning engine.";
        return false;
    }

    private static bool IsGroundPredicate(PredicateApplication predicate)
    {
        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefaultOrEmpty)
        {
            return false;
        }

        return predicate.Arguments.All(IsGroundTerm);
    }

    private static bool IsGroundTerm(SemanticTerm term) =>
        term switch
        {
            IndividualTerm individual =>
                !string.IsNullOrWhiteSpace(individual.Identifier.Value),
            DefiniteDescriptionTerm description =>
                !string.IsNullOrWhiteSpace(description.Head.Value) &&
                !description.Modifiers.IsDefault &&
                description.Modifiers.All(modifier =>
                    !string.IsNullOrWhiteSpace(modifier.Value)),
            _ => false
        };

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
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.Length == right.Arguments.Length &&
        left.Arguments.Zip(right.Arguments).All(pair =>
            SemanticTermComparer.AreEquivalent(pair.First, pair.Second));
}
