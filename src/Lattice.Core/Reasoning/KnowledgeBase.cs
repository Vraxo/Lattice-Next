using System.Collections.Immutable;
using Lattice.Core.Language;
using Lattice.Core.Semantics;

namespace Lattice.Core.Reasoning;

public sealed class KnowledgeBase
{
    private readonly List<KnowledgeFact> facts = [];
    private readonly List<UniversalRule> rules = [];
    private readonly IReadOnlyList<KnowledgeFact> readOnlyFacts;
    private readonly IReadOnlyList<UniversalRule> readOnlyRules;
    private long nextExistentialWitnessId;

    public KnowledgeBase()
    {
        readOnlyFacts = facts.AsReadOnly();
        readOnlyRules = rules.AsReadOnly();
    }

    public IReadOnlyList<KnowledgeFact> Facts => readOnlyFacts;

    public IReadOnlyList<UniversalRule> Rules => readOnlyRules;

    public bool TryAdd(
        SemanticUtterance utterance,
        out KnowledgeEntryKind kind,
        out string? error,
        KnowledgeSourceKind sourceKind = KnowledgeSourceKind.UserAssertion,
        string? sourceId = null)
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

        if (utterance.Meaning is ExistentialQuantification existential &&
            KnowledgeFactFactory.TryGetBoundUnaryPredicates(
                existential.Body,
                existential.Variable,
                out var existentialConditions))
        {
            var witness = new AnonymousIndividualTerm(
                new SemanticIdentifier($"witness-{++nextExistentialWitnessId}"));

            foreach (var fact in KnowledgeFactFactory.CreateExistentialFacts(
                existentialConditions,
                witness,
                utterance.SourceText,
                sourceKind,
                sourceId))
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (KnowledgeFactFactory.TryCreateGroundFacts(
            utterance.Meaning,
            utterance.SourceText,
            utterance.ResolvedReferent,
            sourceKind,
            sourceId,
            out var groundFacts))
        {
            foreach (var fact in groundFacts)
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Fact;
            return true;
        }

        if (UniversalRuleFactory.TryCreate(
            utterance.Meaning,
            utterance.SourceText,
            sourceKind,
            sourceId,
            out var candidateRule,
            out var conclusionDescriptionFacts))
        {
            var existingRuleIndex = rules.FindIndex(rule =>
                rule.ConclusionPolarity == candidateRule.ConclusionPolarity &&
                AreSamePremises(rule.Premises, candidateRule.Premises) &&
                AreSamePremises(rule.NegativePremises, candidateRule.NegativePremises) &&
                AreSamePremises(rule.RelationalPremises, candidateRule.RelationalPremises) &&
                IsSamePredicate(rule.Conclusion, candidateRule.Conclusion));

            if (existingRuleIndex < 0)
            {
                rules.Add(candidateRule);
            }
            else
            {
                var existingRule = rules[existingRuleIndex];
                var sources = existingRule.Sources
                    .Add(new KnowledgeSource(sourceKind, utterance.SourceText, SourceId: sourceId))
                    .Distinct()
                    .ToImmutableArray();
                rules[existingRuleIndex] = existingRule with
                {
                    AdditionalSources = sources.Skip(1).ToImmutableArray()
                };
            }

            foreach (var fact in conclusionDescriptionFacts)
            {
                AddFact(fact);
            }

            kind = KnowledgeEntryKind.Rule;
            return true;
        }

        error =
            "Only ground facts, existential assertions with unary conditions, and universal rules with one or more unary premises and either one unary conclusion or one binary relation to a concrete individual are supported by the current reasoning engine.";
        return false;
    }

    private void AddFact(KnowledgeFact fact)
    {
        var existingFactIndex = facts.FindIndex(existing =>
            existing.Polarity == fact.Polarity &&
            IsSameFact(existing.Predicate, fact.Predicate));

        if (existingFactIndex < 0)
        {
            facts.Add(fact);
            return;
        }

        var existingFact = facts[existingFactIndex];
        var sources = existingFact.Sources
            .AddRange(fact.Sources)
            .Distinct()
            .ToImmutableArray();

        facts[existingFactIndex] = existingFact with
        {
            AdditionalSources = sources.Skip(1).ToImmutableArray()
        };
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


    private static bool IsSameFact(
        PredicateApplication left,
        PredicateApplication right) =>
        left.Predicate == right.Predicate &&
        !left.Arguments.IsDefault &&
        !right.Arguments.IsDefault &&
        left.Arguments.SequenceEqual(right.Arguments);
}

}
