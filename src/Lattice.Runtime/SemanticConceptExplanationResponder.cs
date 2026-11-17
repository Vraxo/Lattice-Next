using System.Collections.Immutable;
using System.Globalization;
using Lattice.Client;
using Lattice.Core.Reasoning;
using Lattice.Core.Semantics;

namespace Lattice.Runtime;
using static Lattice.Runtime.SemanticWorldModelResponseFormatting;
using static Lattice.Runtime.SemanticWorldModelResponseReasoningHelpers;

internal static class SemanticConceptExplanationResponder
{

internal static bool TryWhy(
        string individualId,
        string typeId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        var type = model.FindConcept(typeId, SemanticConceptKind.Type);
        var definition = type is null ? null : model.FindDefinition(type.Id);
        if (individual is null ||
            type is null ||
            definition?.SufficientExpression is null ||
            !TryExplainExpression(
                individual.Id,
                definition.SufficientExpression,
                model,
                reasoner,
                out var phrases,
                out var sources))
        {
            return false;
        }

        message = $"{Capitalize(IndefinitePhrase(individual.Label))} is {IndefinitePhrase(type.Label)} because it {JoinConditions(phrases)}.";
        sources = ImmutableArray.Create(definition.Source).AddRange(sources);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

private static bool TryExplainExpression(
        string individual,
        SemanticClassExpression expression,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out ImmutableArray<string> phrases,
        out ImmutableArray<SemanticWorldModelSource> sources)
    {
        phrases = [];
        sources = [];
        if (expression.Kind == SemanticClassExpressionKind.Some)
        {
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    continue;
                }

                var nestedPhrases = ImmutableArray<string>.Empty;
                var nestedSources = ImmutableArray<SemanticWorldModelSource>.Empty;
                if (expression.Filler is not null &&
                    !TryExplainExpression(
                        candidate.Object,
                        expression.Filler,
                        model,
                        reasoner,
                        out nestedPhrases,
                        out nestedSources))
                {
                    continue;
                }

                var phrase = FormatCondition(
                    new SemanticRestriction(expression.Relation!, expression.TargetType!),
                    model);
                if (phrase is null)
                {
                    return false;
                }

                if (!nestedPhrases.IsDefaultOrEmpty)
                {
                    phrase += $" that {JoinConditions(nestedPhrases)}";
                }

                phrases = [phrase];
                sources = ImmutableArray.Create(candidate.Source)
                    .AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!))
                    .AddRange(nestedSources);
                return true;
            }

            return false;
        }

        if (expression.Kind == SemanticClassExpressionKind.All)
        {
            var relation = model.FindRelation(expression.Relation!);
            var target = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            if (relation is null || target is null)
            {
                return false;
            }

            var assertionSources = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
            foreach (var candidate in reasoner.GetRelations(individual, expression.Relation))
            {
                if (!reasoner.IsInstanceOf(candidate.Object, expression.TargetType!))
                {
                    return false;
                }

                assertionSources.Add(candidate.Source);
                assertionSources.AddRange(reasoner.GetMembershipEvidence(candidate.Object, expression.TargetType!));
                if (expression.Filler is not null)
                {
                    if (!TryExplainExpression(candidate.Object, expression.Filler, model, reasoner,
                        out _, out var nestedSources))
                    {
                        return false;
                    }

                    assertionSources.AddRange(nestedSources);
                }
            }

            var phrase = $"{relation.Verb} only elements of type {target.Label}";
            if (expression.Filler is not null)
            {
                var fillerPhrase = FormatExpression(expression.Filler, model);
                if (fillerPhrase is null)
                {
                    return false;
                }

                phrase += $" that {fillerPhrase}";
            }

            phrases = [phrase];
            sources = assertionSources.Distinct().ToImmutableArray();
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Cycle)
        {
            var startRelation = model.FindRelation(expression.StartRelation!);
            var traversalRelation = model.FindRelation(expression.Relation!);
            var targetType = model.FindConcept(expression.TargetType!, SemanticConceptKind.Type);
            var sourceConnection = expression.SourceConnectionRelation is null
                ? null
                : model.FindRelation(expression.SourceConnectionRelation);
            var targetConnection = expression.TargetConnectionRelation is null
                ? null
                : model.FindRelation(expression.TargetConnectionRelation);
            var cycleSources = reasoner.GetCycleEvidence(individual, expression);
            if (startRelation is null || traversalRelation is null ||
                targetType is null || cycleSources.IsDefaultOrEmpty)
            {
                return false;
            }

            phrases =
            [
                $"{startRelation.Verb} {IndefinitePhrase(targetType.Label)} and follows {traversalRelation.Verb} links for at least {expression.MinimumLength} elements before returning to that same element" +
                (sourceConnection is null || targetConnection is null
                    ? string.Empty
                    : $", with each transition matching {sourceConnection.Verb} to {targetConnection.Verb} by identity")
            ];
            sources = cycleSources;
            return true;
        }

        if (expression.Kind == SemanticClassExpressionKind.Or)
        {
            foreach (var operand in expression.Operands)
            {
                if (TryExplainExpression(individual, operand, model, reasoner, out phrases, out sources))
                {
                    return true;
                }
            }

            return false;
        }

        var phraseBuilder = ImmutableArray.CreateBuilder<string>();
        var sourceBuilder = ImmutableArray.CreateBuilder<SemanticWorldModelSource>();
        foreach (var operand in expression.Operands)
        {
            if (!TryExplainExpression(individual, operand, model, reasoner, out var childPhrases, out var childSources))
            {
                return false;
            }

            phraseBuilder.AddRange(childPhrases);
            sourceBuilder.AddRange(childSources);
        }

        phrases = phraseBuilder.ToImmutable();
        sources = sourceBuilder.ToImmutable();
        return true;
    }

internal static bool TryExplainMechanism(
        string individualId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;
        var individual = model.FindConcept(individualId, SemanticConceptKind.Individual);
        if (individual is null)
        {
            return false;
        }

        var mechanismAssertions = reasoner.GetRelations(individual.Id, "has-sound-production-mechanism");
        if (mechanismAssertions.IsDefaultOrEmpty)
        {
            return false;
        }

        var sentences = new List<string>();
        var sources = new List<SemanticWorldModelSource>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mechanism in mechanismAssertions)
        {
            var relation = model.FindRelation(mechanism.Relation);
            var target = model.FindConcept(mechanism.Object, SemanticConceptKind.Individual);
            if (relation is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(individual, target, relation, first: true));
            sources.Add(mechanism.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, 0);
        }

        if (sentences.Count == 0)
        {
            return false;
        }

        message = string.Join(" ", sentences);
        evidence = ImmutableArray.Create(ToEvidence(sources.Distinct().ToImmutableArray(), LatticeEvidenceKind.Derived));
        return true;
    }

private static void AppendMechanismChain(
        string subjectId,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        HashSet<string> visited,
        List<string> sentences,
        List<SemanticWorldModelSource> sources,
        int depth)
    {
        if (depth >= 6 || !visited.Add(subjectId))
        {
            return;
        }

        foreach (var assertion in reasoner.GetRelations(subjectId)
            .Where(assertion => (model.FindRelation(assertion.Relation)?.Order ?? 0) > 0))
        {
            var relation = model.FindRelation(assertion.Relation);
            var subject = model.FindConcept(assertion.Subject, SemanticConceptKind.Individual);
            var target = model.FindConcept(assertion.Object, SemanticConceptKind.Individual);
            if (relation is null || subject is null || target is null)
            {
                continue;
            }

            sentences.Add(FormatRelation(subject, target, relation, first: false));
            sources.Add(assertion.Source);
            AppendMechanismChain(target.Id, model, reasoner, visited, sentences, sources, depth + 1);
        }
    }

internal static bool TryExplainRequiredKnowledge(
        string requestedActivity,
        string requestedKnowledge,
        SemanticWorldModel model,
        SemanticWorldModelReasoner reasoner,
        out string message,
        out ImmutableArray<LatticeEvidence> evidence)
    {
        message = string.Empty;
        evidence = ImmutableArray<LatticeEvidence>.Empty;

        var activity = model.FindConcept(requestedActivity, SemanticConceptKind.Individual);
        var knowledge = model.FindConcept(requestedKnowledge, SemanticConceptKind.Individual);
        if (activity is null ||
            knowledge is null ||
            !reasoner.IsInstanceOf(activity.Id, "creative-activity") ||
            !reasoner.IsInstanceOf(knowledge.Id, "knowledge-domain"))
        {
            return false;
        }

        var entryRequirements = reasoner.GetRelations(activity.Id, "has-entry-requirement");
        var startingMethods = reasoner.GetRelations(activity.Id, "can-begin-through");
        var helpfulTopics = reasoner.GetRelations(activity.Id, "benefits-from");
        var advancedTopics = reasoner.GetRelations(activity.Id, "later-benefits-from");
        if (entryRequirements.Length != 1 ||
            startingMethods.Length != 3 ||
            helpfulTopics.Length != 4 ||
            advancedTopics.Length != 3)
        {
            return false;
        }

        var entryRequirement = model.FindConcept(entryRequirements[0].Object, SemanticConceptKind.Individual);
        var starts = startingMethods
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var helpful = helpfulTopics
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        var advanced = advancedTopics
            .Select(assertion => model.FindConcept(assertion.Object, SemanticConceptKind.Individual))
            .ToArray();
        if (entryRequirement is null ||
            starts.Any(concept => concept is null) ||
            helpful.Any(concept => concept is null) ||
            advanced.Any(concept => concept is null))
        {
            return false;
        }

        var sources = entryRequirements
            .Concat(startingMethods)
            .Concat(helpfulTopics)
            .Concat(advancedTopics)
            .Select(assertion => assertion.Source)
            .Concat(reasoner.GetMembershipEvidence(activity.Id, "creative-activity"))
            .Concat(reasoner.GetMembershipEvidence(knowledge.Id, "knowledge-domain"))
            .Distinct()
            .ToImmutableArray();

        message = $"You do not need {entryRequirement.Label} to start {activity.Label}. You can begin with {JoinNames(starts.Select(concept => concept!.Label).ToArray())}. Some basic {knowledge.Label}, such as {JoinNames(helpful.Select(concept => concept!.Label).ToArray())}, can help you understand and develop what you hear. As your compositions become more ambitious, {JoinNames(advanced.Select(concept => concept!.Label).ToArray())} can become useful, but they are tools to learn as needed rather than prerequisites. Start composing and add theory as it helps you solve musical problems.";
        evidence = ImmutableArray.Create(ToEvidence(sources, LatticeEvidenceKind.Derived));
        return true;
    }
}
