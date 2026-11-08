using System.Collections.Immutable;
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
            TryGroundPredicate(predicate, out var groundedPredicate, out var descriptionFacts))
        {
            AddFact(new KnowledgeFact(
                groundedPredicate,
                utterance.SourceText,
                utterance.ResolvedReferent));

            foreach (var descriptionFact in descriptionFacts)
            {
                AddFact(new KnowledgeFact(
                    descriptionFact,
                    utterance.SourceText,
                    utterance.ResolvedReferent));
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (utterance.Meaning is UniversalQuantification universal &&
            universal.Body is Implication implication &&
            universal.Variable is { } variable &&
            TryGetRulePremises(implication.Premise, variable, out var premises) &&
            implication.Conclusion is PredicateApplication conclusion &&
            IsBoundUnaryPredicate(conclusion, variable))
        {
            if (!rules.Any(rule =>
                    AreSamePremises(rule.Premises, premises) &&
                    IsSamePredicate(rule.Conclusion, conclusion)))
            {
                rules.Add(new UniversalRule(premises, conclusion, utterance.SourceText));
            }

            kind = KnowledgeEntryKind.Rule;
            return true;
        }

        error =
            "Only ground facts and universal rules with one or more unary premises and one unary conclusion are supported by the current reasoning engine.";
        return false;
    }

    private void AddFact(KnowledgeFact fact)
    {
        if (!facts.Any(existing => IsSameFact(existing.Predicate, fact.Predicate)))
        {
            facts.Add(fact);
        }
    }

    private static bool TryGroundPredicate(
        PredicateApplication predicate,
        out PredicateApplication groundedPredicate,
        out List<PredicateApplication> descriptionFacts)
    {
        groundedPredicate = predicate;
        descriptionFacts = [];

        if (string.IsNullOrWhiteSpace(predicate.Predicate.Value) ||
            predicate.Arguments.IsDefaultOrEmpty)
        {
            return false;
        }

        var arguments = ImmutableArray.CreateBuilder<SemanticTerm>(predicate.Arguments.Length);

        foreach (var argument in predicate.Arguments)
        {
            switch (argument)
            {
                case IndividualTerm individual when
                    !string.IsNullOrWhiteSpace(individual.Identifier.Value):
                    arguments.Add(individual);
                    break;

                case DescriptiveIndividualTerm description
                    when IsValidDescription(description):
                    arguments.Add(new IndividualTerm(description.Identifier));

                    descriptionFacts.Add(CreateUnaryFact(
                        description.Head,
                        description.Identifier));

                    foreach (var modifier in description.Modifiers)
                    {
                        descriptionFacts.Add(CreateUnaryFact(
                            modifier,
                            description.Identifier));
                    }

                    break;

                default:
                    return false;
            }
        }

        groundedPredicate = predicate with
        {
            Arguments = arguments.ToImmutable()
        };

        return true;
    }

    private static bool IsValidDescription(DescriptiveIndividualTerm description) =>
        !string.IsNullOrWhiteSpace(description.Head.Value) &&
        !description.Modifiers.IsDefault &&
        description.Modifiers.All(modifier =>
            !string.IsNullOrWhiteSpace(modifier.Value));

    private static PredicateApplication CreateUnaryFact(
        SemanticIdentifier predicate,
        SemanticIdentifier individual) =>
        new(
            predicate,
            ImmutableArray.Create<SemanticTerm>(new IndividualTerm(individual)));

    private static bool TryGetRulePremises(
        SemanticFormula formula,
        VariableTerm variable,
        out ImmutableArray<PredicateApplication> premises)
    {
        premises = formula switch
        {
            PredicateApplication predicate => ImmutableArray.Create(predicate),
            Conjunction conjunction when !conjunction.Conjuncts.IsDefaultOrEmpty =>
                conjunction.Conjuncts
                    .OfType<PredicateApplication>()
                    .ToImmutableArray(),
            _ => ImmutableArray<PredicateApplication>.Empty
        };

        if (premises.IsDefaultOrEmpty ||
            (formula is Conjunction conjunction &&
             premises.Length != conjunction.Conjuncts.Length))
        {
            return false;
        }

        return premises.All(premise => IsBoundUnaryPredicate(premise, variable));
    }

    private static bool AreSamePremises(
        ImmutableArray<PredicateApplication> left,
        ImmutableArray<PredicateApplication> right) =>
        !left.IsDefault &&
        !right.IsDefault &&
        left.Length == right.Length &&
        left.Zip(right).All(pair => IsSamePredicate(pair.First, pair.Second));

    private static bool IsSamePredicate(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);

    private static bool IsBoundUnaryPredicate(
        PredicateApplication predicate,
        VariableTerm variable) =>
        !string.IsNullOrWhiteSpace(predicate.Predicate.Value) &&
        !string.IsNullOrWhiteSpace(variable.Identifier.Value) &&
        !predicate.Arguments.IsDefault &&
        predicate.Arguments.Length == 1 &&
        predicate.Arguments[0] is VariableTerm argument &&
        argument.Identifier == variable.Identifier;    private static bool IsBoundUnaryPredicate(
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
        left.Arguments.SequenceEqual(right.Arguments);
}
